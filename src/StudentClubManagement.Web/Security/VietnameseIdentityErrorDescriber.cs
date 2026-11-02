using Microsoft.AspNetCore.Identity;

namespace StudentClubManagement.Web.Security;

public class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Email đã được sử dụng.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Email đã được sử dụng.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Email chưa đúng định dạng.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "Email chứa ký tự không được hỗ trợ.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Mật khẩu hiện tại không đúng.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Mật khẩu mới phải có ít nhất {length} ký tự.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Mật khẩu mới phải có ký tự đặc biệt.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Mật khẩu mới phải có chữ số.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Mật khẩu mới phải có chữ thường.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Mật khẩu mới phải có chữ hoa.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Mật khẩu mới phải có ít nhất {uniqueChars} ký tự khác nhau.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Tài khoản vừa được cập nhật ở phiên khác. Vui lòng tải lại trang và thử lại.");
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Không thể cập nhật tài khoản. Vui lòng thử lại.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
