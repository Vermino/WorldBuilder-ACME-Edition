using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using WorldBuilder.Shared.Models;
using WorldBuilder.Lib;

namespace WorldBuilder.Editors.Landscape.ViewModels {
    public partial class BiomeObjectViewModel : ObservableObject {
        private readonly BiomeObject _model;
        private readonly ThumbnailCache _thumbnailCache;

        public BiomeObject Model => _model;

        public uint ObjectId => _model.ObjectId;

        public float Density {
            get => _model.Density;
            set => SetProperty(_model.Density, value, _model, (m, v) => m.Density = v);
        }

        public float MinScale {
            get => _model.MinScale;
            set => SetProperty(_model.MinScale, value, _model, (m, v) => m.MinScale = v);
        }

        public float MaxScale {
            get => _model.MaxScale;
            set => SetProperty(_model.MaxScale, value, _model, (m, v) => m.MaxScale = v);
        }

        public float MinSlope {
            get => _model.MinSlope;
            set => SetProperty(_model.MinSlope, value, _model, (m, v) => m.MinSlope = v);
        }

        public float MaxSlope {
            get => _model.MaxSlope;
            set => SetProperty(_model.MaxSlope, value, _model, (m, v) => m.MaxSlope = v);
        }

        [ObservableProperty]
        private Bitmap? _thumbnail;

        private readonly System.Action<BiomeObjectViewModel> _deleteAction;

        public BiomeObjectViewModel(BiomeObject model, ThumbnailCache thumbnailCache, System.Action<BiomeObjectViewModel> deleteAction) {
            _model = model;
            _thumbnailCache = thumbnailCache;
            _deleteAction = deleteAction;
            LoadThumbnail();
        }

        private void LoadThumbnail() {
            // Setup objects have flag 0x02000000
            bool isSetup = (ObjectId & 0x02000000) != 0;
            _ = Task.Run(async () => {
                var bitmap = await _thumbnailCache.GetThumbnailAsync(ObjectId, isSetup);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Thumbnail = bitmap);
            });
        }

        [RelayCommand]
        private void Delete() {
            _deleteAction(this);
        }
    }
}
