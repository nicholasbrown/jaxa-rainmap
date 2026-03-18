// Leaflet JS Interop for Blazor
window.leafletInterop = {
    map: null,
    currentLayers: [],
    tileLayer: null,

    // Bridge JS logs to C# ILogger
    _log: function (level, category, message) {
        try {
            DotNet.invokeMethodAsync('JaxaRainmap', 'OnJsLog', level, category, message);
        } catch (_) {
            // Fallback if .NET bridge not ready
        }
        // Always also log to browser console for terminal visibility
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

        // Force a resize after init
        // Force a resize after init
        setTimeout(() => this.map.invalidateSize(), 100);
        this._log('info', 'LeafletMap', 'Map initialized at [' + lat + ',' + lon + '] zoom=' + zoom);
    },

    loadCogLayer: async function (cogUrl, paletteType, minVal, maxVal) {
        return await this.loadCogLayers([cogUrl], paletteType, minVal, maxVal);
    },

    loadCogLayers: async function (cogUrls, paletteType, minVal, maxVal) {
        if (!this.map) return false;

        try {
            this.removeCogLayer();

            const colorFn = this._getColorFunction(paletteType, minVal, maxVal);
            let anyLoaded = false;

            for (const cogUrl of cogUrls) {
                try {
                    const response = await fetch(cogUrl);
                    if (!response.ok) {
                        this._log('warn', 'LeafletMap', 'Skipping COG (HTTP ' + response.status + '): ' + cogUrl);
                        continue;
                    }

                    const arrayBuffer = await response.arrayBuffer();
                    const georaster = await parseGeoraster(arrayBuffer);

                    const layer = new GeoRasterLayer({
                        georaster: georaster,
                        opacity: 0.7,
                        pixelValuesToColorFn: colorFn,
                        resolution: 256
                    });

                    layer.addTo(this.map);
                    this.currentLayers.push(layer);
                    anyLoaded = true;
                } catch (tileErr) {
                    this._log('error', 'LeafletMap', 'Error loading COG tile ' + cogUrl + ': ' + tileErr.message);
                }
            }

            return anyLoaded;
        } catch (err) {
            this._log('error', 'LeafletMap', 'Error loading COG layers: ' + err.message);
            return false;
        }
    },

    removeCogLayer: function () {
        if (this.map) {
            for (const layer of this.currentLayers) {
                this.map.removeLayer(layer);
            }
        }
        this.currentLayers = [];
    },

    swapFrame: async function (cogUrls, paletteType, minVal, maxVal) {
        return await this.loadCogLayers(
            Array.isArray(cogUrls) ? cogUrls : [cogUrls],
            paletteType, minVal, maxVal
        );
    },

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
                return null; // transparent
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
