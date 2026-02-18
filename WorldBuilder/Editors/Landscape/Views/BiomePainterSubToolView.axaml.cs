using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WorldBuilder.Editors.Landscape.ViewModels;

namespace WorldBuilder.Editors.Landscape.Views {
    public partial class BiomePainterSubToolView : UserControl {
        public BiomePainterSubToolView() {
            InitializeComponent();
        }

        private void OnDrop(object? sender, DragEventArgs e) {
            if (DataContext is BiomePainterSubToolViewModel vm) {
                if (e.Data.Contains("ObjectBrowserItem")) {
                    var item = e.Data.Get("ObjectBrowserItem") as ObjectBrowserItem;
                    if (item != null) {
                        vm.AddAssetToBiome(item.Id);
                    }
                }
            }
        }
    }
}
