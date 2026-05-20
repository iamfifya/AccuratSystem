// Controls/CustomComboBox.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace AccuratPanelCarWashing.Controls
{
    [ContentProperty("Items")]
    [TemplatePart(Name = "PART_ToggleButton", Type = typeof(ToggleButton))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_ItemsPanel", Type = typeof(StackPanel))]
    [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
    [TemplatePart(Name = "PART_MainBorder", Type = typeof(Border))]
    public class CustomComboBox : ItemsControl
    {
        static CustomComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomComboBox),
                new FrameworkPropertyMetadata(typeof(CustomComboBox)));
        }

        private ToggleButton _toggleButton;
        private Popup _popup;
        private TextBox _textBox;
        private StackPanel _itemsPanel;
        private ScrollViewer _scrollViewer;
        private Border _mainBorder;
        private bool _isDropDownOpen;

        #region Dependency Properties

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SelectedValuePathProperty =
            DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(CustomComboBox),
                new FrameworkPropertyMetadata("Выберите..."));

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(CustomComboBox),
                new FrameworkPropertyMetadata(new CornerRadius(5)));

        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(CustomComboBox));

        #endregion

        #region Properties

        public new object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public object SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set
            {
                _isDropDownOpen = value;
                if (_popup != null)
                    _popup.IsOpen = value;
                if (value)
                    UpdateItemsPanel();
            }
        }

        #endregion

        #region Events

        public event RoutedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        #endregion

        #region Overrides

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _toggleButton = GetTemplateChild("PART_ToggleButton") as ToggleButton;
            _popup = GetTemplateChild("PART_Popup") as Popup;
            _textBox = GetTemplateChild("PART_TextBox") as TextBox;
            _itemsPanel = GetTemplateChild("PART_ItemsPanel") as StackPanel;
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _mainBorder = GetTemplateChild("PART_MainBorder") as Border;

            if (_toggleButton != null)
                _toggleButton.Click += ToggleButton_Click;

            // Делаем весь комбобокс кликабельным
            if (_mainBorder != null)
            {
                _mainBorder.MouseLeftButtonUp += MainBorder_MouseLeftButtonUp;
                _mainBorder.Cursor = Cursors.Hand;
            }

            // Также делаем кликабельным TextBox (если он не редактируемый)
            if (_textBox != null && !IsEditable)
            {
                _textBox.MouseLeftButtonUp += TextBox_MouseLeftButtonUp;
                _textBox.Cursor = Cursors.Hand;
            }

            UpdateDisplay();
        }

        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            if (IsDropDownOpen)
                UpdateItemsPanel();
        }

        #endregion

        #region Event Handlers

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            IsDropDownOpen = !IsDropDownOpen;
        }

        private void MainBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Открываем/закрываем список при клике на любую область комбобокса
            IsDropDownOpen = !IsDropDownOpen;
        }

        private void TextBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Для нередактируемого TextBox - открываем список
            if (!IsEditable)
            {
                IsDropDownOpen = !IsDropDownOpen;
            }
        }

        private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext != null)
            {
                SelectedItem = border.DataContext;
                UpdateSelectedValue(); // ← ДОБАВИТЬ ЭТУ СТРОКУ!
                IsDropDownOpen = false;
                UpdateDisplay();
                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
            }
        }

        #endregion

        #region Private Methods

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var comboBox = d as CustomComboBox;
            System.Diagnostics.Debug.WriteLine($"CustomComboBox OnSelectedItemChanged: old={e.OldValue}, new={e.NewValue}");

            if (comboBox != null)
            {
                comboBox.UpdateSelectedValue();
                comboBox.UpdateDisplay();

                // Принудительно обновляем ItemsPanel если открыт
                if (comboBox.IsDropDownOpen)
                    comboBox.UpdateItemsPanel();
            }
        }

        private void UpdateSelectedValue()
        {
            if (SelectedItem != null && !string.IsNullOrEmpty(SelectedValuePath))
            {
                var prop = SelectedItem.GetType().GetProperty(SelectedValuePath);
                if (prop != null)
                    SelectedValue = prop.GetValue(SelectedItem);
            }
        }

        private void UpdateDisplay()
        {
            if (_textBox == null) return;

            System.Diagnostics.Debug.WriteLine($"UpdateDisplay: SelectedItem={SelectedItem}, SelectedValue={SelectedValue}");

            if (SelectedItem != null)
            {
                _textBox.Text = GetDisplayText(SelectedItem);
                _textBox.Foreground = new SolidColorBrush(Colors.Black);
            }
            else if (SelectedValue != null && ItemsSource != null)
            {
                var source = ItemsSource as IEnumerable;
                if (source != null)
                {
                    foreach (var item in source)
                    {
                        var value = GetItemValue(item);
                        // Сравниваем значения правильно, с учётом типов
                        if (Equals(value, SelectedValue))
                        {
                            SelectedItem = item;
                            _textBox.Text = GetDisplayText(item);
                            _textBox.Foreground = new SolidColorBrush(Colors.Black);
                            return;
                        }
                    }
                }
                _textBox.Text = Placeholder;
                _textBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else if (!string.IsNullOrEmpty(Placeholder))
            {
                _textBox.Text = Placeholder;
                _textBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private object GetItemValue(object item)
        {
            if (item == null) return null;
            if (!string.IsNullOrEmpty(SelectedValuePath))
            {
                var prop = item.GetType().GetProperty(SelectedValuePath);
                if (prop != null)
                    return prop.GetValue(item);
            }
            return item;
        }

        private string GetDisplayText(object item)
        {
            if (item == null) return string.Empty;

            // 1. СНАЧАЛА проверяем, задан ли DisplayMemberPath (мы задали "Value")
            if (!string.IsNullOrEmpty(DisplayMemberPath))
            {
                var prop = item.GetType().GetProperty(DisplayMemberPath);
                if (prop != null)
                {
                    var value = prop.GetValue(item);
                    return value?.ToString() ?? string.Empty;
                }
            }

            // 2. Если это KeyValuePair (Словарь), по умолчанию показываем текст (Value), а не цифру (Key)
            // var type = item.GetType();
            // if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            // {
            //    var valueProp = type.GetProperty("Value"); // Было "Key", стало "Value"!
            //     if (valueProp != null)
            //     {
            //         var val = valueProp.GetValue(item);
            //         return val?.ToString() ?? string.Empty;
            //     }
            // }

            // Если это KeyValuePair (Словарь), показываем Key (текст), а Value используем для SelectedValuePath
            var type = item.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                // Сначала проверяем, задан ли DisplayMemberPath
                if (!string.IsNullOrEmpty(DisplayMemberPath))
                {
                    var prop = type.GetProperty(DisplayMemberPath);
                    if (prop != null)
                    {
                        var value = prop.GetValue(item);
                        return value?.ToString() ?? string.Empty;
                    }
                }
                // По умолчанию для KeyValuePair показываем Key (текст)
                var keyProp = type.GetProperty("Key");
                if (keyProp != null)
                {
                    var val = keyProp.GetValue(item);
                    return val?.ToString() ?? string.Empty;
                }
            }

            // 3. Если это ComboBoxItem (для ручной верстки в XAML)
            if (item is ComboBoxItem comboBoxItem)
            {
                return comboBoxItem.Content?.ToString() ?? string.Empty;
            }

            // 4. Запасной вариант
            return item.ToString();
        }

        private void UpdateItemsPanel()
        {
            if (_itemsPanel == null) return;

            _itemsPanel.Children.Clear();

            var source = ItemsSource?.Cast<object>().ToList() ?? Items.Cast<object>().ToList();

            foreach (var item in source)
            {
                var border = CreateItemBorder(item);
                _itemsPanel.Children.Add(border);
            }

            if (_scrollViewer != null)
            {
                _scrollViewer.MaxHeight = Math.Min(300, _itemsPanel.Children.Count * 40);
            }
        }

        private Border CreateItemBorder(object item)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(10, 10, 10, 10),
                Cursor = Cursors.Hand,
                DataContext = item
            };

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Colors.White);
            };
            border.MouseLeftButtonUp += Item_MouseLeftButtonUp;

            if (ItemTemplate != null)
            {
                var content = new ContentControl
                {
                    ContentTemplate = ItemTemplate,
                    Content = item
                };
                border.Child = content;
            }
            else
            {
                var textBlock = new TextBlock
                {
                    Text = GetDisplayText(item),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13
                };
                border.Child = textBlock;
            }

            return border;
        }

        #endregion
    }
}
