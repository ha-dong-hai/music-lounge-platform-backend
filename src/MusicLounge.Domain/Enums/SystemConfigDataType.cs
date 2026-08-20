// CoreFlow: CF6 (Payment & Revenue), CF7 (Analytics & Reporting)
// Data type of a system_config value — used to parse the stored string correctly.
namespace MusicLounge.Domain.Enums;

public enum SystemConfigDataType
{
    Decimal = 1,
    Integer = 2,
    Boolean = 3,
    String = 4,
    Json = 5
}
