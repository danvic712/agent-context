namespace AgentContext.Application.Localization;

/// <summary>
/// The stable error codes used by <see cref="LocalizedException"/> across the
/// platform (T11). Each code maps to a translation key <c>errors.{code}</c> in
/// the shared JSON resources (<c>locales/{locale}/errors.json</c>) — call sites reference these
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

    public static class Inference
    {
        public const string NotConfigured = "inference.notConfigured";
        public const string NameRequired = "inference.nameRequired";
        public const string ProviderRequired = "inference.providerRequired";
        public const string ConfigurationRequired = "inference.configurationRequired";
        public const string ProviderIdRequired = "inference.providerIdRequired";
        public const string ProviderIdDuplicate = "inference.providerIdDuplicate";
        public const string ProviderNameRequired = "inference.providerNameRequired";
        public const string ProviderTypeUnsupported = "inference.providerTypeUnsupported";
        public const string BaseUrlInvalid = "inference.baseUrlInvalid";
        public const string ApiKeyRequired = "inference.apiKeyRequired";
        public const string RoutesRequired = "inference.routesRequired";
        public const string CapabilitiesRequired = "inference.capabilitiesRequired";
        public const string RouteIdRequired = "inference.routeIdRequired";
        public const string RouteIdDuplicate = "inference.routeIdDuplicate";
        public const string ProviderNotFound = "inference.providerNotFound";
        public const string ModelRequired = "inference.modelRequired";
        public const string ValidationFailed = "inference.validationFailed";
    }

    public static class Skill
    {
        public const string SlugExists = "skill.slugExists";
        public const string SlugInvalid = "skill.slugInvalid";
        public const string NameRequired = "skill.nameRequired";
        public const string NotFound = "skill.notFound";
        public const string SlugNotFound = "skill.slugNotFound";
        public const string PackageExists = "skill.packageExists";
        public const string FilePathInvalid = "skill.filePathInvalid";
        public const string FileNotFound = "skill.fileNotFound";
        public const string FileTooLarge = "skill.fileTooLarge";
        public const string PackageTooLarge = "skill.packageTooLarge";
        public const string ImportInvalid = "skill.importInvalid";
        public const string PageSizeInvalid = "skill.pageSizeInvalid";
        public const string CursorInvalid = "skill.cursorInvalid";
        public const string SortInvalid = "skill.sortInvalid";
    }

    public static class Session
    {
        public const string FullContextRequired = "session.fullContextRequired";
        public const string UsageInvalid = "session.usageInvalid";
        public const string NotFound = "session.notFound";
    }

    public static class Knowledge
    {
        public const string NotFound = "knowledge.notFound";
        public const string NotArchived = "knowledge.notArchived";
        public const string NotActive = "knowledge.notActive";
        public const string ArchivedCannotRate = "knowledge.archivedCannotRate";
        public const string PageSizeInvalid = "knowledge.pageSizeInvalid";
        public const string CursorInvalid = "knowledge.cursorInvalid";
    }

    public static class Platform
    {
        public const string NotConfigured = "platform.notConfigured";
    }

    public static class Settings
    {
        public const string UnsupportedLanguage = "settings.unsupportedLanguage";
        public const string UnsupportedTheme = "settings.unsupportedTheme";
    }
}
