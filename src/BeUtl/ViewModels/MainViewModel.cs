using System.Reactive.Linq;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Threading;

using BeUtl.Collections;
using BeUtl.Configuration;
using BeUtl.Controls;
using BeUtl.Framework;
using BeUtl.Framework.Service;
using BeUtl.Framework.Services;
using BeUtl.Services;

using DynamicData;

using Microsoft.Extensions.DependencyInjection;

using Reactive.Bindings;

namespace BeUtl.ViewModels;

public class MainViewModel
{
    private readonly IProjectService _projectService;
    private readonly EditorService _editorService;
    private readonly PageExtension[] _primitivePageExtensions;
    internal readonly Task _packageLoadTask;

    public sealed class NavItemViewModel
    {
        public NavItemViewModel(PageExtension extension)
        {
            Extension = extension;
            Context = Activator.CreateInstance(extension.Context) ?? throw new Exception("�R���e�L�X�g���쐬�ł��܂���ł����B");
            Header = extension.Header.GetResourceObservable()
                .Select(o => o ?? string.Empty)
                .ToReadOnlyReactivePropertySlim(string.Empty);
        }

        public NavItemViewModel(PageExtension extension, object context)
        {
            Extension = extension;
            Context = context;
            Header = extension.Header.GetResourceObservable()
                .Select(o => o ?? string.Empty)
                .ToReadOnlyReactivePropertySlim(string.Empty);
        }

        public ReadOnlyReactivePropertySlim<string> Header { get; }

        public PageExtension Extension { get; }

        public object Context { get; }
    }

    public MainViewModel()
    {
        _projectService = ServiceLocator.Current.GetRequiredService<IProjectService>();
        _editorService = ServiceLocator.Current.GetRequiredService<EditorService>();
        _primitivePageExtensions = new PageExtension[]
        {
            EditPageExtension.Instance,
            ExtensionPageExtension.Instance,
            OutputPageExtension.Instance,
            SettingsPageExtension.Instance,
        };

        IsProjectOpened = _projectService.IsOpened;

        IObservable<bool> isProjectOpenedAndTabOpened = _projectService.IsOpened
            .CombineLatest(_editorService.SelectedTabItem)
            .Select(i => i.First && i.Second != null);

        // Todo: SelectMany�̎g���������Ă邩�킩���̂Ńe�X�g����
        IObservable<bool> isSceneOpened = _editorService.SelectedTabItem
            .SelectMany(i => i?.Context ?? Observable.Empty<IEditorContext?>())
            .Select(v => v is EditViewModel);

        AddToProject = new(isProjectOpenedAndTabOpened);
        RemoveFromProject = new(isProjectOpenedAndTabOpened);
        AddLayer = new(isSceneOpened);
        DeleteLayer = new(isSceneOpened);
        ExcludeLayer = new(isSceneOpened);
        CutLayer = new(isSceneOpened);
        CopyLayer = new(isSceneOpened);
        PasteLayer = new(isSceneOpened);
        CloseFile = new(_editorService.SelectedTabItem.Select(i => i != null));
        CloseProject = new(_projectService.IsOpened);

        CloseFile.Subscribe(() =>
        {
            EditorTabItem? tabItem = _editorService.SelectedTabItem.Value;
            if (tabItem != null)
            {
                _editorService.CloseTabItem(
                    tabItem.FilePath.Value,
                    tabItem.TabOpenMode);
            }
        });
        CloseProject.Subscribe(() => _projectService.CloseProject());

        _packageLoadTask = Task.Run(async () =>
        {
            PackageManager manager = PackageManager.Instance;
            int id1 = Package.s_nextId++;
            int id2 = Package.s_nextId++;

            manager.LoadPackages(manager.GetPackageInfos());

            manager.ExtensionProvider._allExtensions.Add(id1, _primitivePageExtensions);

            // Todo: ������SceneEditorExtension��o�^���Ă���̂ŁA
            //       �p�b�P�[�W�Ƃ��ĕ�������ꍇ�������폜
            manager.ExtensionProvider._allExtensions.Add(id2, new Extension[]
            {
                SceneEditorExtension.Instance,
                SceneWorkspaceItemExtension.Instance,
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current != null)
                {
                    PackageManager.Instance.AttachToApplication(Application.Current);
                }
            });
        });

        Pages = new()
        {
            new(EditPageExtension.Instance),
            new(ExtensionPageExtension.Instance),
            new(OutputPageExtension.Instance),
        };
        SelectedPage.Value = Pages[0];
        ViewConfig viewConfig = GlobalConfiguration.Instance.ViewConfig;
        viewConfig.RecentFiles.ForEachItem(
            item => RecentFileItems.Insert(0, item),
            item => RecentFileItems.Remove(item),
            () => RecentFileItems.Clear());

        viewConfig.RecentProjects.ForEachItem(
            item => RecentProjectItems.Insert(0, item),
            item => RecentProjectItems.Remove(item),
            () => RecentProjectItems.Clear());

        OpenRecentFile.Subscribe(file => _editorService.ActivateTabItem(file, TabOpenMode.YourSelf));

        OpenRecentProject.Subscribe(file =>
        {
            IProjectService service = ServiceLocator.Current.GetRequiredService<IProjectService>();
            INotificationService noticeService = ServiceLocator.Current.GetRequiredService<INotificationService>();

            if (!File.Exists(file))
            {
                // Todo: ���\�[�X�ɒu������
                noticeService.Show(new Notification(
                    Title: "",
                    Message: "�t�@�C�������݂��Ȃ�"));
            }
            else if (service.OpenProject(file) == null)
            {
                // Todo: ���\�[�X�ɒu������
                noticeService.Show(new Notification(
                    Title: "",
                    Message: "�v���W�F�N�g���J���Ȃ�����"));
            }
        });

        _packageLoadTask.ContinueWith(_ =>
        {
            PackageManager manager = PackageManager.Instance;
            IEnumerable<PageExtension> toAdd
                = manager.ExtensionProvider.AllExtensions.OfType<PageExtension>().Except(_primitivePageExtensions);
            Dispatcher.UIThread.InvokeAsync(() => Pages.AddRange(toAdd.Select(item => new NavItemViewModel(item))));
        });
    }

    // File
    //    Create new
    //       Project
    //       File
    //    Open
    //       Project
    //       File
    //    Close
    //    Close project
    //    Save
    //    Save all
    //    Recent files
    //    Recent projects
    //    Exit
    public ReactiveCommand CreateNewProject { get; } = new();

    public ReactiveCommand CreateNew { get; } = new();

    public ReactiveCommand OpenProject { get; } = new();

    public ReactiveCommand OpenFile { get; } = new();

    public ReactiveCommand CloseFile { get; }

    public ReactiveCommand CloseProject { get; }

    public ReactiveCommand<string> OpenRecentFile { get; } = new();

    public ReactiveCommand<string> OpenRecentProject { get; } = new();

    public CoreList<string> RecentFileItems { get; } = new();

    public CoreList<string> RecentProjectItems { get; } = new();

    public ReactiveCommand Exit { get; } = new();

    // Project
    //    Add
    //    Remove
    public ReactiveCommand AddToProject { get; }

    public ReactiveCommand RemoveFromProject { get; }

    // Scene
    //    New
    //    Settings
    //    Layer
    //       Add
    //       Delete
    //       Exclude
    //       Cut
    //       Copy
    //       Paste
    public ReactiveCommand NewScene { get; } = new();

    public ReactiveCommand AddLayer { get; }

    public ReactiveCommand DeleteLayer { get; }

    public ReactiveCommand ExcludeLayer { get; }

    public ReactiveCommand CutLayer { get; }

    public ReactiveCommand CopyLayer { get; }

    public ReactiveCommand PasteLayer { get; }

    public NavItemViewModel SettingsPage { get; } = new(SettingsPageExtension.Instance);

    public CoreList<NavItemViewModel> Pages { get; }

    public ReactiveProperty<NavItemViewModel?> SelectedPage { get; } = new();

    public IReadOnlyReactiveProperty<bool> IsProjectOpened { get; }
}
