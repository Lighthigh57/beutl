using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

using BeUtl.ViewModels.Dialogs;

using FluentAvalonia.UI.Controls;

namespace BeUtl.Views.Dialogs;

public sealed partial class CreateNewProject : ContentDialog, IStyleable
{
    private IDisposable? _sBtnBinding;

    public CreateNewProject()
    {
        InitializeComponent();
    }

    Type IStyleable.StyleKey => typeof(ContentDialog);

    // �߂�
    protected override void OnPrimaryButtonClick(ContentDialogButtonClickEventArgs args)
    {
        base.OnPrimaryButtonClick(args);
        if (carousel.SelectedIndex == 1)
        {
            args.Cancel = true;
            // '�߂�'�𖳌���
            IsPrimaryButtonEnabled = false;
            // IsSecondaryButtonEnabled�̃o�C���h����
            _sBtnBinding?.Dispose();
            // '�V�K�쐬'��'����'�ɕύX
            SecondaryButtonText = (string?)Application.Current?.FindResource("S.Dialogs.CreateNewProject.Next") ?? string.Empty;
            // '����'��L����
            IsSecondaryButtonEnabled = true;
            carousel.Previous();
        }
    }

    // '����' or '�V�K�쐬'
    protected override void OnSecondaryButtonClick(ContentDialogButtonClickEventArgs args)
    {
        base.OnSecondaryButtonClick(args);
        if (DataContext is not CreateNewProjectViewModel vm) return;

        if (carousel.SelectedIndex == 1)
        {
            vm.Create.Execute();
        }
        else
        {
            args.Cancel = true;

            // '�߂�'��\��
            IsPrimaryButtonEnabled = true;
            // IsSecondaryButtonEnabled��CanCreate���o�C���h
            _sBtnBinding = this.Bind(IsSecondaryButtonEnabledProperty, vm.CanCreate);
            // '����'��'�V�K�쐬�ɕύX'
            SecondaryButtonText = (string?)Application.Current?.FindResource("S.Dialogs.CreateNewProject.CreateNew") ?? string.Empty;
            carousel.Next();
        }
    }

    // �ꏊ��I��
    private async void PickLocation(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateNewProjectViewModel vm && VisualRoot is Window parent)
        {
            var picker = new OpenFolderDialog();

            string? result = await picker.ShowAsync(parent);

            if (result != null)
            {
                vm.Location.Value = result;
            }
        }
    }
}
