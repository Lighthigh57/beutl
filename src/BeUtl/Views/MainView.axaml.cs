using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;

using BeUtl.Controls;
using BeUtl.Framework;
using BeUtl.Framework.Service;
using BeUtl.Framework.Services;
using BeUtl.Pages;
using BeUtl.ProjectSystem;
using BeUtl.Services;
using BeUtl.ViewModels;
using BeUtl.Views.Dialogs;

using FluentAvalonia.Core.ApplicationModel;
using FluentAvalonia.UI.Controls;

using Microsoft.Extensions.DependencyInjection;

namespace BeUtl.Views;

public partial class MainView : UserControl
{
    private readonly EditPage _editPage;

    public MainView()
    {
        InitializeComponent();

        // NavigationView�̐ݒ�
        EditPageItem.Tag = _editPage = new EditPage();
        SettingsPageItem.Tag = new SettingsPage();

        Navi.SelectedItem = EditPageItem;
        Navi.ItemInvoked += NavigationView_ItemInvoked;

        NaviContent.Content = EditPageItem.Tag;

        _editPage.tabview.SelectionChanged += TabView_SelectionChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
        {
            vm.CreateNewProject.Subscribe(async () =>
            {
                var dialog = new CreateNewProject();
                await dialog.ShowAsync();
            });

            vm.OpenProject.Subscribe(async () =>
            {
                IProjectService service = ServiceLocator.Current.GetRequiredService<IProjectService>();

                // Todo: ��Ŋg���q��ύX
                var dialog = new OpenFileDialog
                {
                    Filters =
                    {
                        new FileDialogFilter
                        {
                            Name = Application.Current?.FindResource("S.Common.ProjectFile") as string,
                            Extensions =
                            {
                                "bep"
                            }
                        }
                    }
                };

                if (VisualRoot is Window window)
                {
                    string[]? files = await dialog.ShowAsync(window);
                    if ((files?.Any() ?? false) && File.Exists(files[0]))
                    {
                        service.OpenProject(files[0]);
                    }
                }
            });

            vm.OpenScene.Subscribe(async () =>
            {
                IProjectService service = ServiceLocator.Current.GetRequiredService<IProjectService>();
                Project? project = service.CurrentProject.Value;

                if (VisualRoot is Window window && project != null)
                {
                    // Todo: ��Ŋg���q��ύX
                    var dialog = new OpenFileDialog
                    {
                        Filters =
                        {
                            new FileDialogFilter
                            {
                                Name = Application.Current?.FindResource("S.Common.SceneFile") as string,
                                Extensions =
                                {
                                    "scene"
                                }
                            }
                        }
                    };
                    string[]? files = await dialog.ShowAsync(window);
                    if ((files?.Any() ?? false) && File.Exists(files[0]))
                    {
                        var scene = new Scene();
                        scene.Restore(files[0]);
                        project.Children.Add(scene);
                    }
                }
            });

            vm.AddScene.Subscribe(async () =>
            {
                var dialog = new CreateNewScene();
                await dialog.ShowAsync();
            });

            vm.RemoveScene.Subscribe(async () =>
            {
                IProjectService service = ServiceLocator.Current.GetRequiredService<IProjectService>();
                Project? project = service.CurrentProject.Value;

                if (VisualRoot is Window window
                    && project != null
                    && _editPage.tabview.SelectedContent is EditView editor
                    && editor.DataContext is EditViewModel viewModel)
                {
                    string name = Path.GetFileName(viewModel.Scene.FileName);
                    var dialog = new ContentDialog
                    {
                        [!ContentDialog.CloseButtonTextProperty] = new DynamicResourceExtension("S.Common.Cancel"),
                        [!ContentDialog.PrimaryButtonTextProperty] = new DynamicResourceExtension("S.Common.OK"),
                        DefaultButton = ContentDialogButton.Primary,
                        Content = (Application.Current?.FindResource("S.Message.DoYouWantToExcludeThisSceneFromProject") as string ?? "") + "\n" + name
                    };

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        project.Children.Remove(viewModel.Scene);
                    }
                }
                //TaskDialog
            });

            vm.OpenFile.Subscribe(async () =>
            {
                if (VisualRoot is not Window root)
                {
                    return;
                }

                var dialog = new OpenFileDialog
                {
                    AllowMultiple = true,
                };

                dialog.Filters.AddRange(PackageManager.Instance.ExtensionProvider.AllExtensions
                    .OfType<EditorExtension>()
                    .Select(e => new FileDialogFilter()
                    {
                        Extensions = e.FileExtensions.ToList(),
                        Name = Application.Current?.FindResource(e.FileTypeName.Key) as string
                    }));

                string[]? files = await dialog.ShowAsync(root);
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        _editPage.SelectOrAddTabItem(file);
                    }
                }
            });

            vm.SaveAll.Subscribe(async () =>
            {
                IProjectService service = ServiceLocator.Current.GetRequiredService<IProjectService>();

                Project? project = service.CurrentProject.Value;
                INotificationService nservice = ServiceLocator.Current.GetRequiredService<INotificationService>();
                int itemsCount = 0;

                try
                {
                    project?.Save(project.FileName);
                    itemsCount++;

                    foreach (DraggableTabItem? item in _editPage.tabview.Items.OfType<DraggableTabItem>())
                    {
                        if (item.Content is IEditor editor
                            && editor.Commands != null
                            && await editor.Commands.OnSave())
                        {
                            itemsCount++;
                        }
                    }

                    string message = new ResourceReference<string>("S.Message.ItemsSaved").FindOrDefault(string.Empty);
                    nservice.Show(new Notification(
                        string.Empty,
                        string.Format(message, itemsCount.ToString()),
                        NotificationType.Success));
                }
                catch
                {
                    string message = new ResourceReference<string>("S.Message.OperationCouldNotBeExecuted").FindOrDefault(string.Empty);
                    nservice.Show(new Notification(
                        string.Empty,
                        message,
                        NotificationType.Error));
                }
            });

            vm.Save.Subscribe(async () =>
            {
                if (_editPage.tabview.SelectedContent is IEditor editor && editor.Commands != null)
                {
                    INotificationService nservice = ServiceLocator.Current.GetRequiredService<INotificationService>();
                    try
                    {
                        bool result = await editor.Commands.OnSave();

                        if (result)
                        {
                            string message = new ResourceReference<string>("S.Message.ItemSaved").FindOrDefault("{0}");
                            nservice.Show(new Notification(
                                string.Empty,
                                string.Format(message, Path.GetFileName(editor.EdittingFile) ?? editor.EdittingFile),
                                NotificationType.Success));
                        }
                        else
                        {
                            string message = new ResourceReference<string>("S.Message.OperationCouldNotBeExecuted").FindOrDefault(string.Empty);
                            nservice.Show(new Notification(
                                string.Empty,
                                message,
                                NotificationType.Information));
                        }
                    }
                    catch
                    {
                        string message = new ResourceReference<string>("S.Message.OperationCouldNotBeExecuted").FindOrDefault(string.Empty);
                        nservice.Show(new Notification(
                            string.Empty,
                            message,
                            NotificationType.Error));
                    }

                }
            });

            vm.Exit.Subscribe(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime applicationLifetime)
                {
                    applicationLifetime.Shutdown();
                }
            });
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (e.Root is Window b)
        {
            b.Opened += OnParentWindowOpened;
        }
    }

    private void TabView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && _editPage.tabview.SelectedContent is IEditor editor)
        {
            viewModel.KnownCommands = editor.Commands;
        }
    }

    private void OnParentWindowOpened(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Opened -= OnParentWindowOpened;
        }

        if (sender is CoreWindow cw)
        {
            CoreApplicationViewTitleBar titleBar = cw.TitleBar;
            if (titleBar != null)
            {
                titleBar.ExtendViewIntoTitleBar = true;

                titleBar.LayoutMetricsChanged += OnApplicationTitleBarLayoutMetricsChanged;

                cw.SetTitleBar(Titlebar);
                Titlebar.Margin = new Thickness(0, 0, titleBar.SystemOverlayRightInset, 0);
            }
        }
    }

    private void OnApplicationTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
    {
        Titlebar.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);
    }

    private void NavigationView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is NavigationViewItem item)
        {
            NaviContent.Content = item.Tag;
            e.RecommendedNavigationTransitionInfo.RunAnimation(NaviContent);
        }
    }
}
