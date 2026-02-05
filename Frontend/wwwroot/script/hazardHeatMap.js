window.hazardHeatMap = {
    heatLayer: null,

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
        } catch {
        }
    },

    updateClusters: function (clusters) {
        try {
            if (this.heatLayer === null || clusters === null || clusters === undefined) {
                return;
            }

            if (typeof worldTolatLng !== 'function') {
                return;
            }

            const heatData = [];

            for (let i = 0; i < clusters.length; i++) {
                const c = clusters[i];
                if (c === null || c === undefined) continue;

                const x = (c.x !== undefined) ? c.x : c.X;
                const y = (c.y !== undefined) ? c.y : c.Y;
                const severity = (c.severityScore !== undefined) ? c.severityScore : c.SeverityScore;

                const ll = worldTolatLng(x, y);
                const intensity = Math.max(0, Math.min(1, severity / 100.0));

                heatData.push([ll.lat, ll.lng, intensity]);
            }

            this.heatLayer.setLatLngs(heatData);
        } catch {
        }
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
        } catch {
        }
    }
};
