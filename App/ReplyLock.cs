namespace App;

public class ReplyLock
{
    private string _infoMessage;
    private bool _isLocked = false;

    public bool IsLocked => _isLocked;
    public string InfoMessage => _infoMessage;

    public ReplyLock(string infoMessage)
    {
        _infoMessage = infoMessage;
    }

    public void Lock()
    {
        _isLocked = true;
    }

    public void Unlock()
    {
        _isLocked = false;
    }
}