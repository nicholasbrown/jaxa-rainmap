// Leaflet JS Interop for Blazor — with prebuffered animation
window.leafletInterop = {
    map: null,
    currentLayers: [],
    tileLayer: null,

    // Prebuffer cache: Map<string, georaster>
    _geoCache: new Map(),
    // Animation state
    _animFrames: [],
    _animIndex: 0,
    _animSpeed: 1.0,
    _animRunning: false,
    _animTimerId: null,
    _animPalette: 'jma',
    _animDotNetRef: null,

    // Bridge JS logs to C# ILogger
    _log: function (level, category, message) {
        try {
            DotNet.invokeMethodAsync('JaxaRainmap', 'OnJsLog', level, category, message);
        } catch (_) { }
        var consoleFn = level === 'error' ? console.error : level === 'warn' ? console.warn : console.debug;
        consoleFn('[' + category + ']', message);
    },

    initMap: function (elementId, lat, lon, zoom) {
        if (this.map) {
            this.map.remove();
        }

        this.map = L.map(elementId, {
            center: [lat, lon],
            zoom: zoom,
            zoomControl: true,
            maxBounds: [[-85, -180], [85, 180]],
            maxBoundsViscosity: 1.0
        });

        this.tileLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors | GSMaP &copy; JAXA',
            maxZoom: 18,
            noWrap: true
        }).addTo(this.map);

        setTimeout(() => this.map.invalidateSize(), 100);
        this._log('info', 'LeafletMap', 'Map initialized at [' + lat + ',' + lon + '] zoom=' + zoom);
    },

    // --- Single-frame loading (for initial display / manual stepping) ---

    loadCogLayer: async function (cogUrl, paletteType, minVal, maxVal) {
        return await this.loadCogLayers([cogUrl], paletteType, minVal, maxVal);
    },

    loadCogLayers: async function (cogUrls, paletteType, minVal, maxVal) {
        if (!this.map) return false;

        try {
            const colorFn = this._getColorFunction(paletteType, minVal, maxVal);
            var newLayers = [];

            for (const cogUrl of cogUrls) {
                try {
                    var georaster = this._geoCache.get(cogUrl);

                    if (!georaster) {
                        const response = await fetch(cogUrl);
                        if (!response.ok) {
                            this._log('warn', 'LeafletMap', 'Skipping COG (HTTP ' + response.status + '): ' + cogUrl);
                            continue;
                        }
                        const arrayBuffer = await response.arrayBuffer();
                        georaster = await parseGeoraster(arrayBuffer);
                        this._geoCache.set(cogUrl, georaster);
                    }

                    var layer = new GeoRasterLayer({
                        georaster: georaster,
                        opacity: 0.7,
                        pixelValuesToColorFn: colorFn,
                        resolution: 256
                    });
                    newLayers.push(layer);
                } catch (tileErr) {
                    this._log('error', 'LeafletMap', 'Error loading COG tile ' + cogUrl + ': ' + tileErr.message);
                }
            }

            if (newLayers.length > 0) {
                // Atomic swap in single paint frame
                var self = this;
                requestAnimationFrame(function () {
                    self._removeCurrentLayers();
                    for (const layer of newLayers) {
                        layer.addTo(self.map);
                    }
                    self.currentLayers = newLayers;
                });
            }

            return newLayers.length > 0;
        } catch (err) {
            this._log('error', 'LeafletMap', 'Error loading COG layers: ' + err.message);
            return false;
        }
    },

    _removeCurrentLayers: function () {
        if (this.map) {
            for (const layer of this.currentLayers) {
                this.map.removeLayer(layer);
            }
        }
        this.currentLayers = [];
    },

    removeCogLayer: function () {
        this._removeCurrentLayers();
    },

    swapFrame: async function (cogUrls, paletteType, minVal, maxVal) {
        return await this.loadCogLayers(
            Array.isArray(cogUrls) ? cogUrls : [cogUrls],
            paletteType, minVal, maxVal
        );
    },

    // --- Prebuffer engine ---

    prebufferFrames: async function (frameUrlSets, paletteType) {
        var allUrls = [];
        for (const urlSet of frameUrlSets) {
            for (const url of urlSet) {
                if (!this._geoCache.has(url)) {
                    allUrls.push(url);
                }
            }
        }

        this._log('info', 'Animation', 'Prebuffering ' + allUrls.length + ' COG tiles for ' + frameUrlSets.length + ' frames');
        var loaded = 0;
        var total = allUrls.length;

        for (const url of allUrls) {
            try {
                const response = await fetch(url);
                if (response.ok) {
                    const arrayBuffer = await response.arrayBuffer();
                    const georaster = await parseGeoraster(arrayBuffer);
                    this._geoCache.set(url, georaster);
                } else {
                    this._log('warn', 'Animation', 'Prebuffer skip (HTTP ' + response.status + '): ' + url);
                }
            } catch (err) {
                this._log('warn', 'Animation', 'Prebuffer error: ' + url + ': ' + err.message);
            }
            loaded++;

            try {
                DotNet.invokeMethodAsync('JaxaRainmap', 'OnBufferProgressCallback', loaded, total);
            } catch (_) { }
        }

        this._log('info', 'Animation', 'Prebuffer complete: ' + this._geoCache.size + ' tiles cached');
        return true;
    },

    // Render a frame from the cache (instant, no network)
    renderCachedFrame: function (cogUrls, paletteType) {
        if (!this.map) return false;

        var colorFn = this._getColorFunction(paletteType, 0, 100);
        var newLayers = [];

        for (const url of cogUrls) {
            var georaster = this._geoCache.get(url);
            if (!georaster) continue;

            var layer = new GeoRasterLayer({
                georaster: georaster,
                opacity: 0.7,
                pixelValuesToColorFn: colorFn,
                resolution: 256
            });
            newLayers.push(layer);
        }

        if (newLayers.length > 0) {
            // Atomic swap: remove old + add new in a single requestAnimationFrame
            var self = this;
            requestAnimationFrame(function () {
                self._removeCurrentLayers();
                for (const layer of newLayers) {
                    layer.addTo(self.map);
                }
                self.currentLayers = newLayers;
            });
        }

        return newLayers.length > 0;
    },

    // --- JS-driven animation loop ---

    startAnimation: function (frames, speed, paletteType) {
        this.stopAnimation();

        this._animFrames = frames;
        this._animSpeed = speed;
        this._animPalette = paletteType;
        this._animIndex = 0;
        this._animRunning = true;

        this._log('info', 'Animation', 'Starting playback: ' + frames.length + ' frames at ' + speed + 'x');

        var self = this;
        var interval = 1000.0 / speed;

        if (frames.length > 0) {
            this.renderCachedFrame(frames[0], paletteType);
        }

        this._animTimerId = setInterval(function () {
            if (!self._animRunning || self._animFrames.length === 0) return;

            self._animIndex = (self._animIndex + 1) % self._animFrames.length;
            self.renderCachedFrame(self._animFrames[self._animIndex], self._animPalette);

            try {
                DotNet.invokeMethodAsync('JaxaRainmap', 'OnAnimFrameChangedCallback', self._animIndex);
            } catch (_) { }
        }, interval);
    },

    stopAnimation: function () {
        this._animRunning = false;
        if (this._animTimerId !== null) {
            clearInterval(this._animTimerId);
            this._animTimerId = null;
        }
    },

    setAnimationSpeed: function (speed) {
        if (!this._animRunning) return;
        var frames = this._animFrames;
        var palette = this._animPalette;
        var idx = this._animIndex;
        this.stopAnimation();
        this._animIndex = idx;
        this._animRunning = true;

        var self = this;
        var interval = 1000.0 / speed;
        this._animSpeed = speed;

        this._animTimerId = setInterval(function () {
            if (!self._animRunning || self._animFrames.length === 0) return;
            self._animIndex = (self._animIndex + 1) % self._animFrames.length;
            self.renderCachedFrame(self._animFrames[self._animIndex], self._animPalette);
            try {
                DotNet.invokeMethodAsync('JaxaRainmap', 'OnAnimFrameChangedCallback', self._animIndex);
            } catch (_) { }
        }, interval);
    },

    clearGeoCache: function () {
        this._geoCache.clear();
        this._log('debug', 'Animation', 'Georaster cache cleared');
    },

    // --- Utilities ---

    downloadTextFile: function (filename, text) {
        var blob = new Blob([text], { type: 'text/plain' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    fitBounds: function (south, west, north, east) {
        if (!this.map) return;
        this.map.fitBounds([[south, west], [north, east]]);
    },

    invalidateSize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    },

    _getColorFunction: function (paletteType, minVal, maxVal) {
        const palettes = {
            jma: [
                { threshold: 0, color: [0, 0, 0, 0] },
                { threshold: 0.1, color: [180, 210, 255, 180] },
                { threshold: 1, color: [0, 100, 255, 200] },
                { threshold: 5, color: [0, 200, 100, 210] },
                { threshold: 10, color: [50, 255, 50, 220] },
                { threshold: 20, color: [255, 255, 0, 230] },
                { threshold: 30, color: [255, 180, 0, 240] },
                { threshold: 50, color: [255, 80, 0, 245] },
                { threshold: 80, color: [255, 0, 0, 250] },
                { threshold: 100, color: [180, 0, 180, 255] }
            ],
            viridis: [
                { threshold: 0, color: [0, 0, 0, 0] },
                { threshold: 0.1, color: [68, 1, 84, 180] },
                { threshold: 5, color: [59, 82, 139, 200] },
                { threshold: 15, color: [33, 145, 140, 210] },
                { threshold: 30, color: [94, 201, 98, 230] },
                { threshold: 50, color: [253, 231, 37, 250] }
            ],
            turbo: [
                { threshold: 0, color: [0, 0, 0, 0] },
                { threshold: 0.1, color: [48, 18, 59, 180] },
                { threshold: 5, color: [70, 130, 255, 200] },
                { threshold: 15, color: [40, 230, 130, 210] },
                { threshold: 30, color: [250, 220, 40, 230] },
                { threshold: 50, color: [255, 60, 10, 245] },
                { threshold: 80, color: [122, 4, 3, 255] }
            ]
        };

        const palette = palettes[paletteType] || palettes.jma;

        return function (values) {
            const val = values[0];

            if (val === null || val === undefined || isNaN(val) || val <= 0) {
                return null;
            }

            for (let i = palette.length - 1; i >= 0; i--) {
                if (val >= palette[i].threshold) {
                    const c = palette[i].color;
                    return `rgba(${c[0]},${c[1]},${c[2]},${c[3] / 255})`;
                }
            }

            return null;
        };
    }
};
