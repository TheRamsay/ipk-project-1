namespace App;

public class ThreadSafeBool(bool value)
{
    private bool _value = value;
    private readonly object _lock = new();

    public bool Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
        set
        {
            lock (_lock)
            {
                _value = value;
            }
        }
    }
}