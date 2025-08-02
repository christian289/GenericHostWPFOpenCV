namespace OhMyRudolph.Wpf.Controls.Extensions;

public static class DependencyObjectExtension
{
    public static T? FindAncestor<T>(this DependencyObject dependencyObject)
    where T : DependencyObject
    {
        if (dependencyObject is null) return null;

        var parent = VisualTreeHelper.GetParent(dependencyObject);

        if (parent is T typedParent)
            return typedParent;
        else
            return parent.FindAncestor<T>();
    }

    public static T? FindDescendant<T>(this DependencyObject dependencyObject)
        where T : DependencyObject
    {
        if (dependencyObject is null)
            return null;

        int childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);

        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);

            if (child is T typedChild)
                return typedChild;

            T? foundDescendant = child.FindDescendant<T>();

            if (foundDescendant is not null)
                return foundDescendant;
        }

        return null;
    }

    public static DependencyObject? FindDescendantByName(DependencyObject? parent, string namePart)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject? child = VisualTreeHelper.GetChild(parent, i);

            if (child != null &&
                child is FrameworkElement element &&
                element.GetType().Name.Contains(namePart))
                return element;

            DependencyObject? foundChild = FindDescendantByName(child, namePart);

            if (foundChild is not null)
                return foundChild;
        }

        return null;
    }
}
