# JAXA Rain Radar — GSMaP Precipitation Visualiser

🌧️ A Blazor WebAssembly app that visualises global precipitation data from [JAXA's GSMaP](https://sharaku.eorc.jaxa.jp/GSMaP/) (Global Satellite Mapping of Precipitation) on an interactive map.

![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-blue)
![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- 🗺️ **Interactive global map** powered by Leaflet.js with zoom/pan
- 🌍 **GSMaP precipitation data** rendered directly from Cloud Optimized GeoTIFFs (COGs)
- 📅 **Historical data** with date picker (data available from March 2000)
- ▶️ **Time-lapse animation** with play/pause/speed controls
- 🎨 **Multiple colour palettes** — JMA (default), Viridis, Turbo
- 🌙 **Dark/light theme** toggle
- 📍 **Region quick-jump** — Global, Asia, Japan, Americas, Europe, Africa, Oceania
- 📊 **Multiple datasets** — Daily, Monthly, Climatological Normals

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Getting Started

```bash
# Clone the repository
git clone https://github.com/nicholasbrown/jaxa-rainmap.git
cd jaxa-rainmap

# Restore and run
dotnet run
```

The app will be available at `https://localhost:5001` (or the port shown in the console).

## Build & Publish

```bash
# Build for production
dotnet publish -c Release -o publish

# The output in publish/wwwroot/ is a static site
# Deploy to any static host (GitHub Pages, Azure Static Web Apps, Netlify, etc.)
```

### GitHub Pages Deployment

The published `wwwroot/` folder can be deployed directly to GitHub Pages. Ensure the following files are present:

- `.nojekyll` — prevents Jekyll processing
- `404.html` — copy of `index.html` for SPA routing

## Architecture

```
JaxaRainmap/
├── Program.cs                          # App bootstrap + DI
├── Services/
│   ├── GsmapService.cs                 # STAC catalog + COG URL resolution
│   └── CacheService.cs                 # In-memory LRU cache
├── Models/
│   ├── StacCollection.cs / StacItem.cs # STAC JSON models
│   ├── PrecipitationFrame.cs           # Frame metadata
│   └── MapSettings.cs                  # App state + region presets
├── Components/
│   ├── Pages/Home.razor                # Main page
│   ├── Map/LeafletMap.razor            # Leaflet map (JS Interop)
│   ├── Controls/                       # Date, layer, region, animation controls
│   └── Shared/                         # Legend, loading overlay
└── wwwroot/js/leafletInterop.js        # Leaflet + georaster COG rendering
```

## How It Works

1. **Data Discovery**: The C# `GsmapService` fetches [STAC](https://stacspec.org/) collection metadata from JAXA's Wasabi S3 bucket to discover available timesteps.
2. **COG Rendering**: [georaster-layer-for-leaflet](https://github.com/GeoTIFF/georaster-layer-for-leaflet) reads Cloud Optimized GeoTIFFs directly in the browser via HTTP range requests — no server-side processing needed.
3. **JS Interop**: Blazor communicates with Leaflet.js through a JavaScript interop bridge (`leafletInterop.js`).
4. **Animation**: A C# timer drives frame-by-frame playback, swapping COG layers via JS Interop.

## Data Sources

| Dataset | Collection ID | Resolution |
|---------|--------------|------------|
| Daily Precipitation | `JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily` | 0.1° (~10km) |
| Monthly Precipitation | `JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_monthly` | 0.1° |
| Monthly Normal | `JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_monthly-normal` | 0.1° |
| Half-Monthly Normal | `JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_half-monthly-normal` | 0.1° |

## Data Attribution

> Global Rainfall Map (GSMaP) by JAXA Global Rainfall Watch was produced and distributed by the Earth Observation Research Center (EORC), Japan Aerospace Exploration Agency (JAXA).

- [JAXA Global Rainfall Watch](https://sharaku.eorc.jaxa.jp/GSMaP/)
- [JAXA Earth API](https://data.earth.jaxa.jp/en/)

## License

MIT
