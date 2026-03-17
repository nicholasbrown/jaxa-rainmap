// Leaflet JS Interop for Blazor
window.leafletInterop = {
    map: null,
    currentLayer: null,
    tileLayer: null,

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
    },

    loadCogLayer: async function (cogUrl, paletteType, minVal, maxVal) {
        if (!this.map) return false;

        try {
            this.removeCogLayer();

            const response = await fetch(cogUrl);
            if (!response.ok) {
                console.error('Failed to fetch COG:', response.status, cogUrl);
                return false;
            }

            const arrayBuffer = await response.arrayBuffer();
            const georaster = await parseGeoraster(arrayBuffer);

            const colorFn = this._getColorFunction(paletteType, minVal, maxVal);

            this.currentLayer = new GeoRasterLayer({
                georaster: georaster,
                opacity: 0.7,
                pixelValuesToColorFn: colorFn,
                resolution: 256
            });

            this.currentLayer.addTo(this.map);
            return true;
        } catch (err) {
            console.error('Error loading COG layer:', err);
            return false;
        }
    },

    removeCogLayer: function () {
        if (this.currentLayer && this.map) {
            this.map.removeLayer(this.currentLayer);
            this.currentLayer = null;
        }
    },

    swapFrame: async function (cogUrl, paletteType, minVal, maxVal) {
        return await this.loadCogLayer(cogUrl, paletteType, minVal, maxVal);
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
