using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace AccuratPanelCWD.Controls
{
    [ContentProperty("Items")]
    [TemplatePart(Name = "PART_DropDownToggle", Type = typeof(ToggleButton))]
        [TemplatePart(Name = "PART_EditableTextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    public class CustomComboBox : ComboBox
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(CustomComboBox),
                new FrameworkPropertyMetadata("Выберите..."));

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header),
                typeof(string),
                typeof(CustomComboBox),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(CustomComboBox),
                new FrameworkPropertyMetadata(new CornerRadius(8)));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(
                nameof(DisplayText),
                typeof(string),
                typeof(CustomComboBox),
                new FrameworkPropertyMetadata(string.Empty));

        static CustomComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CustomComboBox),
                new FrameworkPropertyMetadata(typeof(CustomComboBox)));
        }

        public CustomComboBox()
        {
            SetResourceReference(ItemContainerStyleProperty, "CustomComboBoxItemStyle");
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

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value ?? string.Empty);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            UpdateDisplayText();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == DisplayMemberPathProperty ||
                e.Property == SelectedItemProperty ||
                e.Property == ItemTemplateProperty)
            {
                UpdateDisplayText();

                if (e.Property == DisplayMemberPathProperty || e.Property == ItemTemplateProperty)
                    RefreshRealizedItemContainers();
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is not ComboBoxItem comboItem)
                return;

            // Preserve manually-created ComboBoxItem instances.
            if (item is ComboBoxItem)
                return;

            // If the consumer supplied ItemTemplate, let WPF render it.
            if (ItemTemplate != null)
                return;

            // WPF's internal DisplayMemberPath presentation is not reliably
            // surfaced through a fully custom ComboBoxItem template. Resolve
            // the member explicitly so both the popup and the selected field
            // always show the human-readable value instead of Type.ToString().
            if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
                comboItem.Content = GetDisplayText(item);
            else
                comboItem.Content = item;
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            if (element is ComboBoxItem comboItem && item is not ComboBoxItem)
            {
                // Clear stale content before WPF recycles this container.
                comboItem.Content = null;
            }

            base.ClearContainerForItemOverride(element, item);
        }

        /// <summary>
        /// Clears selection and editable text. Kept for backwards compatibility
        /// with existing windows.
        /// </summary>
        public void ClearSelection()
        {
            SelectedItem = null;
            SelectedValue = null;
            SelectedIndex = -1;
            Text = string.Empty;
            UpdateDisplayText();
        }

        private void RefreshRealizedItemContainers()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var container = ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;
                if (container == null)
                    continue;

                var item = Items[i];
                if (item is ComboBoxItem)
                    continue;

                if (ItemTemplate != null)
                    container.Content = item;
                else if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
                    container.Content = GetDisplayText(item);
                else
                    container.Content = item;
            }
        }

        private Border _mainBorder;
        private ToggleButton _dropDownToggle;
        private TextBox _editableTextBox;

        public override void OnApplyTemplate()
        {
            // Detach handlers from the previous template before WPF replaces it.
            if (_mainBorder != null)
                _mainBorder.PreviewMouseLeftButtonDown -= MainBorder_PreviewMouseLeftButtonDown;

            if (_dropDownToggle != null)
                _dropDownToggle.Click -= DropDownToggle_Click;

            base.OnApplyTemplate();

            _mainBorder = GetTemplateChild("PART_MainBorder") as Border;
            _dropDownToggle = GetTemplateChild("PART_DropDownToggle") as ToggleButton;
            _editableTextBox = GetTemplateChild("PART_EditableTextBox") as TextBox;

            if (_mainBorder != null)
                _mainBorder.PreviewMouseLeftButtonDown += MainBorder_PreviewMouseLeftButtonDown;

            if (_dropDownToggle != null)
                _dropDownToggle.Click += DropDownToggle_Click;

            UpdateDisplayText();
        }

        private void MainBorder_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (IsEditable)
                return;

            // The arrow toggle owns its own click. Do not toggle a second time
            // when the mouse event tunnels through the main border.
            var source = e.OriginalSource as DependencyObject;
            if (_dropDownToggle != null && source != null &&
                FindAncestor<ToggleButton>(source) == _dropDownToggle)
                return;

            IsDropDownOpen = !IsDropDownOpen;
            e.Handled = true;
        }

        private void DropDownToggle_Click(object sender, RoutedEventArgs e)
        {
            // IsChecked is two-way bound to IsDropDownOpen by the template.
            // Mark the event handled so the main border cannot toggle it again.
            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void UpdateDisplayText()
        {
            DisplayText = GetDisplayText(SelectedItem);
        }

        private string GetDisplayText(object item)
        {
            if (item == null)
                return string.Empty;

            if (item is ComboBoxItem comboBoxItem)
                return comboBoxItem.Content?.ToString() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
            {
                var value = GetPropertyPathValue(item, DisplayMemberPath);
                if (value != null)
                    return value.ToString();
            }

            var itemType = item.GetType();
            if (itemType.IsGenericType &&
                itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var key = itemType.GetProperty("Key")?.GetValue(item);
                return key?.ToString() ?? string.Empty;
            }

            return item.ToString() ?? string.Empty;
        }

        private static object GetPropertyPathValue(object source, string path)
        {
            if (source == null || string.IsNullOrWhiteSpace(path))
                return null;

            object current = source;

            foreach (var segment in path.Split('.'))
            {
                if (current == null)
                    return null;

                var property = current.GetType().GetProperty(
                    segment,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

                if (property == null)
                    return null;

                current = property.GetValue(current);
            }

            return current;
        }
    }
}
