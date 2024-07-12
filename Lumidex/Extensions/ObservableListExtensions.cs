namespace System.Collections.ObjectModel;

public static class ObservableListExtensions
{
    public static void AddRange<T>(this ObservableCollectionEx<T> collection, IEnumerable<T> items)
    {
        using (var temp = collection.DelayNotifications())
        {
            foreach (var item in items)
            {
                temp.Add(item);
            }
        }
    }
}
