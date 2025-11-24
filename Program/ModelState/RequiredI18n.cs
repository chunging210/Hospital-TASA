using System.ComponentModel.DataAnnotations;

namespace TASA.Program.ModelState
{
    public class RequiredI18n : RequiredAttribute
    {
        private readonly string? _conditionalMethodName;

        public RequiredI18n()
        {
            ErrorMessage = I18nMessgae.RequiredErrorMessage;
        }

        public RequiredI18n(string conditionalMethodName) : this()
        {
            _conditionalMethodName = conditionalMethodName;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var isRequired = ConditionalMethod.IsRequired(_conditionalMethodName, validationContext);
            if (isRequired)
            {
                return base.IsValid(value, validationContext);
            }
            else
            {
                return ValidationResult.Success;
            }
        }
    }
}
