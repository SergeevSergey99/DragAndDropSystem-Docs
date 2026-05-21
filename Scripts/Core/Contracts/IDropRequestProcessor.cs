namespace UDND.Core
{
    public interface IDropRequestProcessor : IDropProcessor
    {
        bool CanAcceptDrop(DragContext context, DropRequestPolicy? requested);
        DropResult ProcessDrop(DragContext context, DropRequestPolicy? requested);
    }
}
