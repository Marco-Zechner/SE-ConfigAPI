namespace MarcoZechner.ConfigAPI.V2.Api
{
    public interface IWorldConfigAuthorization
    {
        bool IsAdmin(ulong playerId);
    }
}