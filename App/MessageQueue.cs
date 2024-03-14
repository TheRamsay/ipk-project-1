using App.Models;

namespace App;

public class MessageQueue
{
    private readonly Queue<Task> _messageQueue = new();
    
    private int _semaphore = 0;
    private bool Enabled => _semaphore == 0;

    public async Task EnqueueMessageAsync(Task task)
    {
        _messageQueue.Enqueue(task);
        
        if (!Enabled)
        {
            return;
        }
        
        var message = _messageQueue.Dequeue();
        Lock();
        await message;
    }
    
    public async Task DequeueMessageAsync()
    {
        if (IsEmpty() || !Enabled)
        {
            return;
        }
        
        var message = _messageQueue.Dequeue();
        Lock();
        await message;
    }

    private bool IsEmpty()
    {
        return _messageQueue.Count == 0;
    }
    
    public void Unlock()
    {
        _semaphore--;
    }
    
    public void Lock()
    {
        _semaphore++;
    }
}