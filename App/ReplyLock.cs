namespace App;

public class ReplyLock
{
    private string _infoMessage;
    private bool _isLocked = false;
    public Semaphore Semaphore { get; set; }

    public bool IsLocked => _isLocked;
    public string InfoMessage => _infoMessage;

    public ReplyLock(string infoMessage)
    {
        _infoMessage = infoMessage;
        this.Semaphore = new Semaphore(0, 2);
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