using System.Windows;
using System.Windows.Controls;

namespace AiTrader.Wiz;

public partial class SecretFieldControl : UserControl
{
    private bool _isSynchronizing;

    public static readonly RoutedEvent ValueChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ValueChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SecretFieldControl));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(SecretFieldControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public SecretFieldControl()
    {
        InitializeComponent();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsRevealed { get; private set; }

    public event RoutedEventHandler ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SecretFieldControl control)
        {
            return;
        }

        control.SyncInputs(e.NewValue as string ?? string.Empty);
        control.RaiseEvent(new RoutedEventArgs(ValueChangedEvent, control));
    }

    private void SyncInputs(string value)
    {
        if (_isSynchronizing)
        {
            return;
        }

        _isSynchronizing = true;
        MaskedInput.Password = value;
        VisibleInput.Text = value;
        _isSynchronizing = false;
    }

    private void MaskedInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizing || IsRevealed)
        {
            return;
        }

        _isSynchronizing = true;
        Value = MaskedInput.Password;
        VisibleInput.Text = MaskedInput.Password;
        _isSynchronizing = false;
    }

    private void VisibleInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizing || !IsRevealed)
        {
            return;
        }

        _isSynchronizing = true;
        Value = VisibleInput.Text;
        MaskedInput.Password = VisibleInput.Text;
        _isSynchronizing = false;
    }

    private void ToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        IsRevealed = !IsRevealed;
        MaskedInput.Visibility = IsRevealed ? Visibility.Collapsed : Visibility.Visible;
        VisibleInput.Visibility = IsRevealed ? Visibility.Visible : Visibility.Collapsed;
    }
}
