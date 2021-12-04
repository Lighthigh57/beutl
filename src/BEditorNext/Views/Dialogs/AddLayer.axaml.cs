using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

using BEditorNext.ViewModels.Dialogs;

using FluentAvalonia.UI.Controls;

namespace BEditorNext.Views.Dialogs;

public sealed partial class AddLayer : ContentDialog, IStyleable
{
    private IDisposable? _sBtnBinding;

    public AddLayer()
    {
        InitializeComponent();
    }

    Type IStyleable.StyleKey=>typeof(ContentDialog);

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
            // '�ǉ�'��'����'�ɕύX
            SecondaryButtonText = (string?)Application.Current.FindResource("NextString") ?? string.Empty;
            // '����'��L����
            IsSecondaryButtonEnabled = true;
            carousel.Previous();
        }
    }

    // '����' or '�ǉ�'
    protected override void OnSecondaryButtonClick(ContentDialogButtonClickEventArgs args)
    {
        base.OnSecondaryButtonClick(args);
        if (DataContext is not AddLayerViewModel vm) return;

        if (carousel.SelectedIndex == 1)
        {
            vm.Add.Execute();
        }
        else
        {
            args.Cancel = true;

            // '�߂�'��\��
            IsPrimaryButtonEnabled = true;
            // IsSecondaryButtonEnabled��CanCreate���o�C���h
            _sBtnBinding = this.Bind(IsSecondaryButtonEnabledProperty, vm.CanAdd);
            // '����'��'�ǉ�'�ɕύX
            SecondaryButtonText = (string?)Application.Current.FindResource("AddString") ?? string.Empty;
            carousel.Next();
        }
    }
}
