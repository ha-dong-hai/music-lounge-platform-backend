namespace MusicLounge.Domain.Enums;

public enum SettlementReleaseType
{
    Partial70,  // D3: first tranche — pre_rate% of net (e.g. 70%)
    Final30,    // D3: second tranche — remaining post_rate% (e.g. 30%)

    // MLACP-350: F&B online — mot tranche duy nhat 100%, chi len lich khi don da DONG (da phuc vu va
    // da tra tien). Khong qua chot thoi luong buoi dien (D16): mon duoc giao tai ban, khong phai buoi
    // dien. Khong thu hoa hong F&B (tien de da ghi o UpdateFnbOrderStatusCommandHandler).
    Full
}
