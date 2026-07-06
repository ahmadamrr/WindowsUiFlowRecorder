namespace WindowsUiFlowRecorder.Presentation.Scanner;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsUiFlowRecorder.Domain.Entities;

public class ElementTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isVisible = true;

    public ElementInfo Element { get; }
    public string DisplayText => BuildDisplayText();
    public int Depth { get; }
    public ObservableCollection<ElementTreeNode> Children { get; } = [];
    public ElementTreeNode? Parent { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(Visibility)); } }
    }

    public Visibility Visibility =>
        _isVisible ? Visibility.Visible : Visibility.Collapsed;

    public ElementTreeNode(ElementInfo element, int depth, ElementTreeNode? parent)
    {
        Element = element;
        Depth = depth;
        Parent = parent;
        foreach (var child in element.Children)
            Children.Add(new ElementTreeNode(child, depth + 1, this));
    }

    private string BuildDisplayText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Element.AutomationId))
            parts.Add($"[{Element.AutomationId}]");
        if (!string.IsNullOrEmpty(Element.Name))
            parts.Add(Element.Name.Length > 60 ? Element.Name[..60] + "..." : Element.Name);
        parts.Add($"({Element.ControlType})");
        if (Element.IsOffscreen)
            parts.Add("[offscreen]");
        return string.Join(" ", parts);
    }

    public IEnumerable<ElementTreeNode> Flatten()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.Flatten())
                yield return descendant;
        }
    }

    public void ApplyFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            SetAllVisible(true);
            return;
        }

        var lower = filter.ToLowerInvariant();
        foreach (var node in Flatten())
        {
            var matches = (node.Element.AutomationId?.ToLowerInvariant().Contains(lower) ?? false)
                || (node.Element.Name?.ToLowerInvariant().Contains(lower) ?? false)
                || (node.Element.ControlType?.ToLowerInvariant().Contains(lower) ?? false)
                || (node.Element.ClassName?.ToLowerInvariant().Contains(lower) ?? false);

            node.IsVisible = matches;

            if (matches && node.Parent != null)
            {
                var p = node.Parent;
                while (p != null)
                {
                    p.IsVisible = true;
                    p.IsExpanded = true;
                    p = p.Parent;
                }
            }
        }
    }

    private void SetAllVisible(bool visible)
    {
        foreach (var node in Flatten())
            node.IsVisible = visible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}