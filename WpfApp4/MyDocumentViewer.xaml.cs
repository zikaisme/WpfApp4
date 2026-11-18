using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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

        // ===== README 6：第三方 ColorPicker 套用文字顏色 =====

        private void FontColorPicker_SelectedColorChanged(
            object? sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (!_isReady || _isUpdatingToolbar || !e.NewValue.HasValue)
                return;

            SolidColorBrush brush = new SolidColorBrush(e.NewValue.Value);
            ApplySelectionProperty(TextElement.ForegroundProperty, brush);
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
            // 選取文字時套用至選取範圍；只有游標時，設定接下來輸入的格式。
            // ComboBox / ColorPicker 取得焦點時，RichTextBox 仍保留原選取範圍。
            MainRichTextBox.Selection.ApplyPropertyValue(property, value);
            MainRichTextBox.Focus();
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
            if (!_isReady || _isUpdatingToolbar)
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
