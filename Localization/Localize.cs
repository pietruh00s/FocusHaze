using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FocusHaze.Localization;

/// <summary>
/// Attached property for XAML: <c>loc:Localize.Key="Intensity_Title"</c> fills a TextBlock's Text
/// (or a ContentControl's Content) and keeps it updated when the language changes at runtime.
/// </summary>
public sealed class Localize
{
    private static readonly List<WeakReference<DependencyObject>> s_targets = [];

    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(Localize), new PropertyMetadata(null, OnKeyChanged));

    public static string GetKey(DependencyObject element) => (string)element.GetValue(KeyProperty);

    public static void SetKey(DependencyObject element, string value) => element.SetValue(KeyProperty, value);

    /// <summary>Re-applies strings to every live element after a language switch.</summary>
    public static void Refresh()
    {
        s_targets.RemoveAll(target => !target.TryGetTarget(out _));
        foreach (var target in s_targets)
        {
            if (target.TryGetTarget(out var element))
                Apply(element);
        }
    }

    private static void OnKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        s_targets.Add(new WeakReference<DependencyObject>(element));
        Apply(element);
    }

    private static void Apply(DependencyObject element)
    {
        if (GetKey(element) is not { Length: > 0 } key)
            return;

        string text = Loc.Get(key);
        switch (element)
        {
            case TextBlock textBlock:
                textBlock.Text = text;
                break;
            case ContentControl contentControl:
                contentControl.Content = text;
                break;
        }
    }
}
