namespace Mz.ConfigApi
{
    public enum ConfigValueKind
    {
        Null = 0,
        Boolean = 1,
        Integer = 2,
        Float = 3,
        String = 4,
        Object = 5,
        Array = 6,
        OffsetDateTime = 7,
        LocalDateTime = 8,
        LocalDate = 9,
        LocalTime = 10,
    }
}