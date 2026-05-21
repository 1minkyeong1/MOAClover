using Microsoft.AspNetCore.Identity;

namespace MOAClover.Services
{
    public class KoreanIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresUpper),
                Description = "영문 대문자를 최소 1개 포함해야 합니다."
            };
        }

        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresLower),
                Description = "영문 소문자를 최소 1개 포함해야 합니다."
            };
        }

        public override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresDigit),
                Description = "숫자를 최소 1개 포함해야 합니다."
            };
        }

        public override IdentityError PasswordTooShort(int length)
        {
            return new IdentityError
            {
                Code = nameof(PasswordTooShort),
                Description = $"비밀번호는 최소 {length}자 이상이어야 합니다."
            };
        }
    }
}