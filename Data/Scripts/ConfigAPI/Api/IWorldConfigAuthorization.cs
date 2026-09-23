namespace MarcoZechner.ConfigAPI.Api
{
    public interface IWorldConfigAuthorization
    {
        bool IsAdmin(ulong playerId);
    }
}