namespace AgentContext.Application.Localization;

/// <summary>
/// The stable error codes used by <see cref="LocalizedException"/> across the
/// platform (T11). Each code maps to a translation key <c>errors.{code}</c> in
/// the shared JSON store (<c>i18n/{locale}.json</c>) — call sites reference these
/// constants so codes never drift from the resource files.
/// </summary>
public static class ErrorCodes
{
    public const string InternalError = "internal.error";

    public static class Setup
    {
        public const string DisplayNameRequired = "setup.displayNameRequired";
        public const string EmailInvalid = "setup.emailInvalid";
        public const string PasswordTooShort = "setup.passwordTooShort";
        public const string AlreadyConfigured = "setup.alreadyConfigured";
    }

    public static class Llm
    {
        public const string BaseUrlRequired = "llm.baseUrlRequired";
        public const string BaseUrlInvalid = "llm.baseUrlInvalid";
        public const string ApiKeyRequired = "llm.apiKeyRequired";
        public const string ModelRequired = "llm.modelRequired";
        public const string NotConfigured = "llm.notConfigured";
    }

    public static class Skill
    {
        public const string SlugExists = "skill.slugExists";
        public const string SlugInvalid = "skill.slugInvalid";
        public const string NameRequired = "skill.nameRequired";
        public const string NotFound = "skill.notFound";
        public const string SlugNotFound = "skill.slugNotFound";
    }

    public static class Pricing
    {
        public const string ModelRequired = "pricing.modelRequired";
        public const string NegativeCost = "pricing.negativeCost";
    }

    public static class Session
    {
        public const string FullContextRequired = "session.fullContextRequired";
        public const string NotFound = "session.notFound";
    }

    public static class Knowledge
    {
        public const string NotFound = "knowledge.notFound";
        public const string NotArchived = "knowledge.notArchived";
    }

    public static class Platform
    {
        public const string NotConfigured = "platform.notConfigured";
    }

    public static class Settings
    {
        public const string UnsupportedLanguage = "settings.unsupportedLanguage";
    }
}
