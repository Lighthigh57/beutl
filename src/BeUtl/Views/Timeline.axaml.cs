using System.Numerics;
using System.Text.Json.Nodes;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

using BeUtl.Collections;
using BeUtl.Models;
using BeUtl.ProjectSystem;
using BeUtl.ViewModels;
using BeUtl.ViewModels.Dialogs;
using BeUtl.Views.Dialogs;

namespace BeUtl.Views;

public partial class Timeline : UserControl
{
    internal enum MouseFlags
    {
        MouseUp,
        MouseDown
    }

    internal MouseFlags _seekbarMouseFlag = MouseFlags.MouseUp;
    internal TimeSpan _pointerFrame;
    internal int _pointerLayer;
    private bool _isFirst = true;
    private TimelineViewModel? _viewModel;
    private IDisposable? _disposable0;
    private IDisposable? _disposable1;
    private IDisposable? _disposable2;

    public Timeline()
    {
        InitializeComponent();

        gridSplitter.DragDelta += GridSplitter_DragDelta;

        ContentScroll.ScrollChanged += ContentScroll_ScrollChanged;
        ContentScroll.AddHandler(PointerWheelChangedEvent, ContentScroll_PointerWheelChanged, RoutingStrategies.Tunnel);
        ScaleScroll.AddHandler(PointerWheelChangedEvent, ContentScroll_PointerWheelChanged, RoutingStrategies.Tunnel);

        TimelinePanel.AddHandler(DragDrop.DragOverEvent, TimelinePanel_DragOver);
        TimelinePanel.AddHandler(DragDrop.DropEvent, TimelinePanel_Drop);
        DragDrop.SetAllowDrop(TimelinePanel, true);
    }

    internal TimelineViewModel ViewModel => _viewModel!;

    private void GridSplitter_DragDelta(object? sender, VectorEventArgs e)
    {
        ColumnDefinition def = grid.ColumnDefinitions[0];
        double last = def.ActualWidth + e.Vector.X;

        if (last is < 395 and > 385)
        {
            def.MaxWidth = 390;
            def.MinWidth = 390;
        }
        else
        {
            def.MaxWidth = double.PositiveInfinity;
            def.MinWidth = 200;
        }
    }

    // DataContext���ύX���ꂽ
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TimelineViewModel vm && vm != _viewModel)
        {
            if (_viewModel != null)
            {
                TimelinePanel.Children.RemoveRange(3, TimelinePanel.Children.Count - 3);

                _disposable0?.Dispose();
                _disposable1?.Dispose();
                _disposable2?.Dispose();
            }

            _viewModel = vm;

            var minHeightBinding = new Binding("TimelineOptions")
            {
                Source = ViewModel.Scene,
                Converter = new FuncValueConverter<TimelineOptions, double>(x => x.MaxLayerCount * Helper.LayerHeight)
            };
            TimelinePanel[!MinHeightProperty] = minHeightBinding;
            LeftPanel[!MinHeightProperty] = minHeightBinding;

            _disposable0 = vm.Layers.ForEachItem(
                AddLayer,
                RemoveLayer,
                () => { });

            _disposable1 = ViewModel.Paste.Subscribe(async () =>
            {
                if (Application.Current?.Clipboard is IClipboard clipboard)
                {
                    string[] formats = await clipboard.GetFormatsAsync();

                    if (formats.AsSpan().Contains(BeUtlDataFormats.Layer))
                    {
                        string json = await clipboard.GetTextAsync();
                        var layer = new Layer();
                        layer.FromJson(JsonNode.Parse(json)!);
                        layer.Start = ViewModel.ClickedFrame;
                        layer.ZIndex = ViewModel.ClickedLayer;

                        layer.Save(Helper.RandomLayerFileName(Path.GetDirectoryName(ViewModel.Scene.FileName)!, "layer"));

                        ViewModel.Scene.AddChild(layer).DoAndRecord(CommandRecorder.Default);
                    }
                }
            });

            _disposable2 = ViewModel.Scene.GetPropertyChangedObservable(Scene.SelectedItemProperty).Subscribe(e =>
            {
                if (e.OldValue != null && FindLayerView(e.OldValue) is TimelineLayer oldView)
                    oldView.border.BorderThickness = new Thickness(0);

                if (e.NewValue != null && FindLayerView(e.NewValue) is TimelineLayer newView)
                    newView.border.BorderThickness = new Thickness(1);
            });
        }
    }

    // PaneScroll���X�N���[�����ꂽ
    private void PaneScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        ContentScroll.Offset = ContentScroll.Offset.WithY(PaneScroll.Offset.Y);
    }

    // ContentScroll���X�N���[�����ꂽ
    private void ContentScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        Scene scene = ViewModel.Scene;
        if (_isFirst)
        {
            ContentScroll.Offset = new(scene.TimelineOptions.Offset.X, scene.TimelineOptions.Offset.Y);
            PaneScroll.Offset = new(0, scene.TimelineOptions.Offset.Y);

            _isFirst = false;
        }

        scene.TimelineOptions = scene.TimelineOptions with
        {
            Offset = new Vector2((float)ContentScroll.Offset.X, (float)ContentScroll.Offset.Y)
        };

        ScaleScroll.Offset = new(ContentScroll.Offset.X, 0);
        PaneScroll.Offset = PaneScroll.Offset.WithY(ContentScroll.Offset.Y);
    }

    // �}�E�X�z�C�[����������
    private void ContentScroll_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        Scene scene = ViewModel.Scene;
        Avalonia.Vector offset = ContentScroll.Offset;

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            // �ڐ���̃X�P�[����ύX
            float scale = scene.TimelineOptions.Scale;
            var ts = offset.X.ToTimeSpan(scale);
            float deltaScale = (float)(e.Delta.Y / 120) * 10 * scale;
            scene.TimelineOptions = scene.TimelineOptions with
            {
                Scale = deltaScale + scale,
            };

            offset = offset.WithX(ts.ToPixel(scene.TimelineOptions.Scale));
        }
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            // �I�t�Z�b�g(Y) ���X�N���[��
            offset = offset.WithY(offset.Y - (e.Delta.Y * 50));
        }
        else
        {
            // �I�t�Z�b�g(X) ���X�N���[��
            offset = offset.WithX(offset.X - (e.Delta.Y * 50));
        }

        ContentScroll.Offset = offset;
        e.Handled = true;
    }

    // �|�C���^�[�ړ�
    private void TimelinePanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        PointerPoint pointerPt = e.GetCurrentPoint(TimelinePanel);
        _pointerFrame = pointerPt.Position.X.ToTimeSpan(ViewModel.Scene.TimelineOptions.Scale)
            .RoundToRate(ViewModel.Scene.Parent is Project proj ? proj.FrameRate : 30);
        _pointerLayer = pointerPt.Position.Y.ToLayerNumber();

        if (_seekbarMouseFlag == MouseFlags.MouseDown)
        {
            ViewModel.Scene.CurrentFrame = _pointerFrame;
        }
    }

    // �|�C���^�[�������ꂽ
    private void TimelinePanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PointerPoint pointerPt = e.GetCurrentPoint(TimelinePanel);

        if (pointerPt.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            _seekbarMouseFlag = MouseFlags.MouseUp;
        }
    }

    // �|�C���^�[�������ꂽ
    private void TimelinePanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint pointerPt = e.GetCurrentPoint(TimelinePanel);
        ViewModel.ClickedFrame = pointerPt.Position.X.ToTimeSpan(ViewModel.Scene.TimelineOptions.Scale)
            .RoundToRate(ViewModel.Scene.Parent is Project proj ? proj.FrameRate : 30);
        ViewModel.ClickedLayer = pointerPt.Position.Y.ToLayerNumber();
        TimelinePanel.Focus();

        if (pointerPt.Properties.IsLeftButtonPressed)
        {
            _seekbarMouseFlag = MouseFlags.MouseDown;
            ViewModel.Scene.CurrentFrame = ViewModel.ClickedFrame;
        }
    }

    // �|�C���^�[�����ꂽ
    private void TimelinePanel_PointerLeave(object? sender, PointerEventArgs e)
    {
        _seekbarMouseFlag = MouseFlags.MouseUp;
    }

    // �h���b�v���ꂽ
    private async void TimelinePanel_Drop(object? sender, DragEventArgs e)
    {
        TimelinePanel.Cursor = Cursors.Arrow;
        Scene scene = ViewModel.Scene;
        Point pt = e.GetPosition(TimelinePanel);

        ViewModel.ClickedFrame = pt.X.ToTimeSpan(scene.TimelineOptions.Scale)
            .RoundToRate(ViewModel.Scene.Parent is Project proj ? proj.FrameRate : 30);
        ViewModel.ClickedLayer = pt.Y.ToLayerNumber();

        if (e.Data.Get("RenderOperation") is LayerOperationRegistry.RegistryItem item)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                var dialog = new AddLayer
                {
                    DataContext = new AddLayerViewModel(scene, new LayerDescription(ViewModel.ClickedFrame, TimeSpan.FromSeconds(5), ViewModel.ClickedLayer, item))
                };
                await dialog.ShowAsync();
            }
            else
            {
                ViewModel.AddLayer.Execute(new LayerDescription(
                    ViewModel.ClickedFrame, TimeSpan.FromSeconds(5), ViewModel.ClickedLayer, item));
            }
        }
    }

    private void TimelinePanel_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("RenderOperation") || (e.Data.GetFileNames()?.Any() ?? false))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    // ���C���[��ǉ�
    private async void AddLayerClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AddLayer
        {
            DataContext = new AddLayerViewModel(ViewModel.Scene,
                new LayerDescription(ViewModel.ClickedFrame, TimeSpan.FromSeconds(5), ViewModel.ClickedLayer))
        };
        await dialog.ShowAsync();
    }

    private async void ShowSceneSettings(object? sender, RoutedEventArgs e)
    {
        var dialog = new SceneSettings()
        {
            DataContext = new SceneSettingsViewModel(ViewModel.Scene)
        };
        await dialog.ShowAsync();
    }

    // ���C���[��ǉ�
    private void AddLayer(int index, TimelineLayerViewModel viewModel)
    {
        var view = new TimelineLayer
        {
            DataContext = viewModel
        };

        TimelinePanel.Children.Add(view);

        LeftPanel.Children.Add(new LayerHeader
        {
            DataContext = viewModel
        });
    }

    // ���C���[���폜
    private void RemoveLayer(int index, TimelineLayerViewModel viewModel)
    {
        Layer layer = viewModel.Model;

        for (int i = 0; i < TimelinePanel.Children.Count; i++)
        {
            IControl item = TimelinePanel.Children[i];
            if (item.DataContext is TimelineLayerViewModel vm && vm.Model == layer)
            {
                TimelinePanel.Children.RemoveAt(i);
                break;
            }
        }

        for (int i = 0; i < LeftPanel.Children.Count; i++)
        {
            IControl item = LeftPanel.Children[i];
            if (item.DataContext is TimelineLayerViewModel vm && vm.Model == layer)
            {
                LeftPanel.Children.RemoveAt(i);
                break;
            }
        }
    }

    private TimelineLayer? FindLayerView(Layer layer)
    {
        return TimelinePanel.Children.FirstOrDefault(ctr => ctr.DataContext is TimelineLayerViewModel vm && vm.Model == layer) as TimelineLayer;
    }
}
