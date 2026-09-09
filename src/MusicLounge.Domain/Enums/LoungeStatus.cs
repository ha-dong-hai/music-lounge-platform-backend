namespace MusicLounge.Domain.Enums;

public enum LoungeStatus
{
    Pending,
    Approved,
    Warned,
    Suspended,
    Locked,
    // MLACP-307. Truoc day Admin khong co cach nao ghi lai mot quyet dinh tu choi: ho so nop len
    // roi nam mai o Pending. Cot Status luu dang chuoi (HasConversion<string>) nen them gia tri
    // vao cuoi la an toan, khong lam xe dich gia tri nao dang co trong DB.
    Rejected
}
