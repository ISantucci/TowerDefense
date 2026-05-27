public interface ICostCommand : ICommand
{
    int  Cost   { get; }
    bool IsDone { get; }
}
