using App.Models;

namespace App;

public class MessageQueue
{
    private readonly Queue<Task> _messageQueue = new();
    
    public int _semaphore = 0;
    private bool Enabled => _semaphore == 0;

    public async Task EnqueueMessageAsync(Task task)
    {
        Console.WriteLine("Enqueuing message before if, semaphore: " + _semaphore);
        if (!Enabled)
        {
            _messageQueue.Enqueue(task);
            return;
        }
        
        Console.WriteLine("Enqueued message, awaiting... locking the queue, semaphore: " + _semaphore);
        Lock();
        await task;
    }
    
    public async Task DequeueMessageAsync()
    {
        if (IsEmpty() || !Enabled)
        {
            return;
        }
        
        var message = _messageQueue.Dequeue();
        Console.WriteLine("Dequeued message, awaiting... locking the queue, semaphore: " + _semaphore);
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