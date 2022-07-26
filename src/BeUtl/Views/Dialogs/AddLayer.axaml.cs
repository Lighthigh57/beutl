﻿using Avalonia;
using Avalonia.Styling;

using BeUtl.ViewModels.Dialogs;

using FluentAvalonia.UI.Controls;

namespace BeUtl.Views.Dialogs;

public sealed partial class AddLayer : ContentDialog, IStyleable
{
    private IDisposable? _sBtnBinding;

    public AddLayer()
    {
        InitializeComponent();
    }

    Type IStyleable.StyleKey => typeof(ContentDialog);

    // 戻る
    protected override void OnPrimaryButtonClick(ContentDialogButtonClickEventArgs args)
    {
        base.OnPrimaryButtonClick(args);
        if (carousel.SelectedIndex == 1)
        {
            args.Cancel = true;
            // '戻る'を無効化
            IsPrimaryButtonEnabled = false;
            // IsSecondaryButtonEnabledのバインド解除
            _sBtnBinding?.Dispose();
            // '追加'を'次へ'に変更
            SecondaryButtonText = S.Dialogs.AddLayer.Next;
            // '次へ'を有効化
            IsSecondaryButtonEnabled = true;
            carousel.Previous();
        }
    }

    // '次へ' or '追加'
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

            // '戻る'を表示
            IsPrimaryButtonEnabled = true;
            // IsSecondaryButtonEnabledとCanCreateをバインド
            _sBtnBinding = this.Bind(IsSecondaryButtonEnabledProperty, vm.CanAdd);
            // '次へ'を'追加'に変更
            SecondaryButtonText = S.Dialogs.AddLayer.Add;
            carousel.Next();
        }
    }
}
