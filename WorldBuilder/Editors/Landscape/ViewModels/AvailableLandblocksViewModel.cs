using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WorldBuilder.Editors.Landscape;
using WorldBuilder.Lib;
using WorldBuilder.ViewModels;

namespace WorldBuilder.Editors.Landscape.ViewModels {
    public partial class AvailableLandblocksViewModel : ViewModelBase {
        private readonly AvailableLandblockFinder _finder;
        private readonly TerrainSystem _terrainSystem;

        [ObservableProperty]
        private ObservableCollection<ushort> _availableLandblocks = new();

        [ObservableProperty]
        private ushort? _selectedLandblock;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private int _scanProgress;

        public AvailableLandblocksViewModel(AvailableLandblockFinder finder, TerrainSystem terrainSystem) {
            _finder = finder;
            _terrainSystem = terrainSystem;
        }

        [RelayCommand]
        private async Task Scan() {
            if (IsScanning) return;
            IsScanning = true;
            ScanProgress = 0;
            AvailableLandblocks.Clear();

            var progress = new Progress<int>(percent => {
                ScanProgress = percent;
            });

            try {
                var results = await _finder.FindAvailableLandblocksAsync(progress);
                foreach (var lbId in results) {
                    AvailableLandblocks.Add(lbId);
                }
            }
            finally {
                IsScanning = false;
                ScanProgress = 100;
            }
        }

        [RelayCommand]
        private void GoToSelected() {
            if (SelectedLandblock == null) return;
            NavigateToLandblock(SelectedLandblock.Value);
        }

        private void NavigateToLandblock(ushort landblockId) {
            var lbX = (landblockId >> 8) & 0xFF;
            var lbY = landblockId & 0xFF;

            const float landblockLength = TerrainDataManager.LandblockLength;

            var centerX = lbX * landblockLength + landblockLength / 2f;
            var centerY = lbY * landblockLength + landblockLength / 2f;

            // Get height at center
            var height = _terrainSystem.Scene.DataManager.GetHeightAtPosition(centerX, centerY);

            var camera = _terrainSystem.Scene.CameraManager.Current;
            if (camera is OrthographicTopDownCamera ortho) {
                // Ortho needs to set position specifically, LookAt might not work as expected for Ortho if Z is fixed?
                // Actually Camera.LookAt usually sets position + target.
                ortho.SetPosition(new Vector3(centerX, centerY, height + 1000f));
            }
            else {
                // Position perspective camera above the landblock center, looking down at it
                camera.SetPosition(new Vector3(centerX, centerY, height + 200f));
                camera.LookAt(new Vector3(centerX, centerY, height));
            }
        }
    }
}
