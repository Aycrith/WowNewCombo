window.hazardHeatMap = {
    heatLayer: null,
    eventLayer: null,
    clusterLayer: null,

    initialize: function () {
        try {
            if (typeof L === 'undefined' || typeof L.heatLayer !== 'function') {
                return;
            }

            if (typeof LeafletMap === 'undefined' || LeafletMap === null) {
                return;
            }

            if (this.heatLayer !== null) {
                return;
            }

            this.heatLayer = L.heatLayer([], {
                radius: 25,
                blur: 15,
                maxZoom: 17,
                gradient: {
                    0.0: '#00ff00',
                    0.4: '#ffff00',
                    0.6: '#ffa500',
                    0.8: '#ff0000',
                    1.0: '#8b0000'
                }
            });

            this.eventLayer = L.layerGroup();
            this.clusterLayer = L.layerGroup();
        } catch {
        }
    },

    updateData: function (points) {
        try {
            if (this.heatLayer === null || points === null || points === undefined) {
                return;
            }

            if (typeof worldTolatLng !== 'function') {
                return;
            }

            if (this.eventLayer !== null) {
                this.eventLayer.clearLayers();
            }

            if (this.clusterLayer !== null) {
                this.clusterLayer.clearLayers();
            }

            const heatData = [];

            for (let i = 0; i < points.length; i++) {
                const p = points[i];
                if (p === null || p === undefined) continue;

                const x = (p.x !== undefined) ? p.x : p.X;
                const y = (p.y !== undefined) ? p.y : p.Y;
                const rawIntensity = (p.intensity !== undefined)
                    ? p.intensity
                    : ((p.Intensity !== undefined)
                        ? p.Intensity
                        : ((p.severityScore !== undefined) ? p.severityScore / 10.0 : ((p.SeverityScore !== undefined) ? p.SeverityScore / 10.0 : 0.5)));
                const categoryValue = (p.category !== undefined) ? p.category : p.Category;
                const category = (typeof categoryValue === 'string') ? categoryValue.toLowerCase() : 'cluster';

                const ll = worldTolatLng(x, y);
                const intensity = Math.max(0.15, Math.min(1, rawIntensity));

                heatData.push([ll.lat, ll.lng, intensity]);

                if (category === 'event' && this.eventLayer !== null) {
                    const eventColor = intensity >= 0.85 ? '#d00000' : '#ff6b00';
                    L.circleMarker(ll, {
                        radius: 2 + (intensity * 4),
                        color: '#111111',
                        weight: 1,
                        fillColor: eventColor,
                        fillOpacity: 0.9
                    }).addTo(this.eventLayer);
                }
                else if (category === 'cluster' && this.clusterLayer !== null) {
                    L.circleMarker(ll, {
                        radius: 4 + (intensity * 6),
                        color: '#ffd166',
                        weight: 2,
                        fillColor: '#ffb703',
                        fillOpacity: 0.15
                    }).addTo(this.clusterLayer);
                }
            }

            this.heatLayer.setLatLngs(heatData);
        } catch {
        }
    },

    updateClusters: function (clusters) {
        this.updateData(clusters);
    },

    show: function () {
        try {
            if (this.heatLayer === null) {
                this.initialize();
            }

            if (this.heatLayer === null) {
                return;
            }

            if (typeof LeafletMap === 'undefined' || LeafletMap === null) {
                return;
            }

            if (!LeafletMap.hasLayer(this.heatLayer)) {
                this.heatLayer.addTo(LeafletMap);
            }

            if (this.clusterLayer !== null && !LeafletMap.hasLayer(this.clusterLayer)) {
                this.clusterLayer.addTo(LeafletMap);
            }

            if (this.eventLayer !== null && !LeafletMap.hasLayer(this.eventLayer)) {
                this.eventLayer.addTo(LeafletMap);
            }
        } catch {
        }
    },

    hide: function () {
        try {
            if (this.heatLayer === null) {
                return;
            }

            if (typeof LeafletMap === 'undefined' || LeafletMap === null) {
                return;
            }

            if (LeafletMap.hasLayer(this.heatLayer)) {
                LeafletMap.removeLayer(this.heatLayer);
            }

            if (this.clusterLayer !== null && LeafletMap.hasLayer(this.clusterLayer)) {
                LeafletMap.removeLayer(this.clusterLayer);
            }

            if (this.eventLayer !== null && LeafletMap.hasLayer(this.eventLayer)) {
                LeafletMap.removeLayer(this.eventLayer);
            }
        } catch {
        }
    }
};
