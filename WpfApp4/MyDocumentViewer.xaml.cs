using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfApp4
{
    public partial class MyDocumentViewer : Window
    {
        // 自訂命令由 XAML 的 Window.CommandBindings 接到處理函式。
        // Ctrl+C / Ctrl+X / Ctrl+V 仍由 RichTextBox 的內建快捷鍵處理。
        public static readonly RoutedUICommand CopyCommand =
            new RoutedUICommand("複製", "CopyCommand", typeof(MyDocumentViewer));
        public static readonly RoutedUICommand CutCommand =
            new RoutedUICommand("剪下", "CutCommand", typeof(MyDocumentViewer));
        public static readonly RoutedUICommand PasteCommand =
            new RoutedUICommand("貼上", "PasteCommand", typeof(MyDocumentViewer));

        private bool _isReady;
        private bool _isUpdatingToolbar;
        private bool _isColorPickerOpen;
        private Color? _colorAtOpen;
        private TextPointer? _colorSelectionStart;
        private TextPointer? _colorSelectionEnd;
        private int _focusRequestVersion;
        private readonly List<FontFamily> _fontFamilies;
        private readonly double[] _fontSizes =
        {
            8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26,
            28, 32, 36, 40, 48, 60, 72, 96
        };

        public MyDocumentViewer()
        {
            // InitializeComponent 期間可能觸發 SelectionChanged，
            // 因此所有事件都先檢查 _isReady。
            _fontFamilies = Fonts.SystemFontFamilies
                .OrderBy(font => font.Source, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            InitializeComponent();

            _isUpdatingToolbar = true;
            FontFamilyComboBox.ItemsSource = _fontFamilies;
            FontSizeComboBox.ItemsSource = _fontSizes;
            _isUpdatingToolbar = false;

            _isReady = true;
            Loaded += MyDocumentViewer_Loaded;
        }

        private void MyDocumentViewer_Loaded(object? sender, RoutedEventArgs e)
        {
            MainRichTextBox.Focus();
            UpdateToolbarFromSelection();
        }

        // ===== README 4：CommandBinding 基本編輯 =====

        private void CopyCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _isReady && !MainRichTextBox.Selection.IsEmpty;
            e.Handled = true;
        }

        private void CopyCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            MainRichTextBox.Copy();
            MainRichTextBox.Focus();
            e.Handled = true;
        }

        private void CutCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _isReady && !MainRichTextBox.IsReadOnly
                && !MainRichTextBox.Selection.IsEmpty;
            e.Handled = true;
        }

        private void CutCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            MainRichTextBox.Cut();
            MainRichTextBox.Focus();
            e.Handled = true;
        }

        private void PasteCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            // 查詢內建 Paste 命令，讓 WPF 判斷剪貼簿是否有可貼上的內容。
            e.CanExecute = _isReady && !MainRichTextBox.IsReadOnly
                && ApplicationCommands.Paste.CanExecute(null, MainRichTextBox);
            e.Handled = true;
        }

        private void PasteCommand_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            MainRichTextBox.Paste();
            MainRichTextBox.Focus();
            e.Handled = true;
        }

        // 選色期間只讓 ColorPicker 處理色盤，不立即搶回鍵盤焦點。
        private void FontColorPicker_Opened(object? sender, RoutedEventArgs e)
        {
            if (!_isReady || !ReferenceEquals(e.OriginalSource, FontColorPicker))
                return;

            _focusRequestVersion++;
            _colorAtOpen = FontColorPicker.SelectedColor;
            _colorSelectionStart = MainRichTextBox.Selection.Start;
            _colorSelectionEnd = MainRichTextBox.Selection.End;
            _isColorPickerOpen = true;
        }

        private void FontColorPicker_Closed(object? sender, RoutedEventArgs e)
        {
            if (!_isReady || !_isColorPickerOpen
                || !ReferenceEquals(e.OriginalSource, FontColorPicker))
                return;

            // 使用關閉時的最終顏色：Esc 取消時，Toolkit 會還原原色。
            Color? chosenColor = FontColorPicker.SelectedColor;
            bool colorChanged = chosenColor.HasValue && chosenColor != _colorAtOpen;
            TextPointer? start = _colorSelectionStart;
            TextPointer? end = _colorSelectionEnd;
            _isColorPickerOpen = false;
            _colorSelectionStart = null;
            _colorSelectionEnd = null;

            int requestVersion = ++_focusRequestVersion;
            // Toolkit 關閉色盤後還會將焦點設回自身按鈕。
            // 排到事件完成之後，才能讓鍵盤焦點真正留在 RichTextBox。
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (requestVersion != _focusRequestVersion || !IsVisible
                    || !IsActive || FontColorPicker.IsOpen)
                    return;

                if (colorChanged && chosenColor.HasValue && start != null && end != null)
                {
                    _isUpdatingToolbar = true;
                    try
                    {
                        MainRichTextBox.Focus();
                        Keyboard.Focus(MainRichTextBox);
                        MainRichTextBox.Selection.Select(start, end);
                        Color color = chosenColor.Value;
                        // 文字顏色使用不透明 RGB，避免透明字造成「打字看不見」。
                        MainRichTextBox.Selection.ApplyPropertyValue(
                            TextElement.ForegroundProperty,
                            new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)));
                    }
                    finally
                    {
                        _isUpdatingToolbar = false;
                    }
                }
                else
                {
                    MainRichTextBox.Focus();
                    Keyboard.Focus(MainRichTextBox);
                }

                UpdateToolbarFromSelection();
            }));
        }

        // 一鍵清除全文，保留原本的 FlowDocument 與復原紀錄。
        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isReady || MainRichTextBox.IsReadOnly)
                return;

            _focusRequestVersion++;
            _isUpdatingToolbar = true;
            MainRichTextBox.Focus();
            Keyboard.Focus(MainRichTextBox);
            MainRichTextBox.BeginChange();
            try
            {
                MainRichTextBox.SelectAll();
                MainRichTextBox.Selection.Text = string.Empty;
                MainRichTextBox.CaretPosition =
                    MainRichTextBox.Document.ContentStart.GetInsertionPosition(
                        LogicalDirection.Forward) ?? MainRichTextBox.Document.ContentStart;

                // 清空後以預設格式輸入，避免沿用之前的透明色或大字。
                TextSelection selection = MainRichTextBox.Selection;
                selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
                selection.ApplyPropertyValue(TextElement.FontFamilyProperty,
                    MainRichTextBox.Document.FontFamily);
                selection.ApplyPropertyValue(TextElement.FontSizeProperty,
                    MainRichTextBox.Document.FontSize);
                selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
                    new TextDecorationCollection());
            }
            finally
            {
                try
                {
                    MainRichTextBox.EndChange();
                }
                finally
                {
                    _isUpdatingToolbar = false;
                }
            }

            UpdateToolbarFromSelection();
            CommandManager.InvalidateRequerySuggested();
        }

        private void FontComboBox_DropDownClosed(object? sender, EventArgs e)
        {
            if (!_isReady)
                return;

            int requestVersion = ++_focusRequestVersion;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (requestVersion != _focusRequestVersion || !IsVisible
                    || !IsActive || FontColorPicker.IsOpen
                    || FontFamilyComboBox.IsDropDownOpen || FontSizeComboBox.IsDropDownOpen)
                    return;

                MainRichTextBox.Focus();
                Keyboard.Focus(MainRichTextBox);
            }));
        }

        // ===== README 7：選擇文字字型 =====

        private void FontFamilyComboBox_SelectionChanged(
            object? sender, SelectionChangedEventArgs e)
        {
            if (!_isReady || _isUpdatingToolbar)
                return;

            FontFamily? font = FontFamilyComboBox.SelectedItem as FontFamily;
            if (font != null)
                ApplySelectionProperty(TextElement.FontFamilyProperty, font);
        }

        // ===== README 8：選擇文字大小 =====

        private void FontSizeComboBox_SelectionChanged(
            object? sender, SelectionChangedEventArgs e)
        {
            if (!_isReady || _isUpdatingToolbar)
                return;

            if (FontSizeComboBox.SelectedItem is double)
            {
                double size = (double)FontSizeComboBox.SelectedItem;
                ApplySelectionProperty(TextElement.FontSizeProperty, size);
            }
        }

        private void ApplySelectionProperty(DependencyProperty property, object value)
        {
            // 先還原編輯焦點，再設定插入格式，避免焦點切換覆寫新格式。
            _isUpdatingToolbar = true;
            try
            {
                MainRichTextBox.Focus();
                Keyboard.Focus(MainRichTextBox);
                MainRichTextBox.Selection.ApplyPropertyValue(property, value);
            }
            finally
            {
                _isUpdatingToolbar = false;
            }
            UpdateToolbarFromSelection();
        }

        private void MainRichTextBox_SelectionChanged(object? sender, RoutedEventArgs e)
        {
            if (!_isReady)
                return;

            UpdateToolbarFromSelection();
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateToolbarFromSelection()
        {
            if (!_isReady || _isUpdatingToolbar || _isColorPickerOpen)
                return;

            // 更新選單時，避免 SelectionChanged 再把格式套用回文件。
            _isUpdatingToolbar = true;
            try
            {
                TextSelection selection = MainRichTextBox.Selection;

                object weight = selection.GetPropertyValue(TextElement.FontWeightProperty);
                BoldButton.IsChecked = weight is FontWeight
                    && ((FontWeight)weight).Equals(FontWeights.Bold);

                object style = selection.GetPropertyValue(TextElement.FontStyleProperty);
                ItalicButton.IsChecked = style is FontStyle
                    && ((FontStyle)style).Equals(FontStyles.Italic);

                TextDecorationCollection? decorations =
                    selection.GetPropertyValue(Inline.TextDecorationsProperty)
                    as TextDecorationCollection;
                UnderlineButton.IsChecked = decorations != null
                    && decorations.Any(item =>
                        item.Location == TextDecorationLocation.Underline);

                FontFamily? family = selection.GetPropertyValue(
                    TextElement.FontFamilyProperty) as FontFamily;
                FontFamilyComboBox.SelectedItem = family == null ? null :
                    _fontFamilies.FirstOrDefault(item =>
                        string.Equals(item.Source, family.Source,
                            StringComparison.OrdinalIgnoreCase));

                object size = selection.GetPropertyValue(TextElement.FontSizeProperty);
                FontSizeComboBox.SelectedItem = size is double ? size : null;

                SolidColorBrush? brush = selection.GetPropertyValue(
                    TextElement.ForegroundProperty) as SolidColorBrush;
                FontColorPicker.SelectedColor = brush == null
                    ? (Color?)null : brush.Color;
            }
            finally
            {
                _isUpdatingToolbar = false;
            }
        }
    }
}