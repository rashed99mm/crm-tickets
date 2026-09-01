namespace CustomerSupport.Application.Errors;

public static class ApplicationErrors
{
    public static class Auth
    {
        public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        public const string INVALID_TOKEN = "INVALID_TOKEN";
        public const string INVALID_REFRESH_TOKEN = "INVALID_REFRESH_TOKEN";
        public const string ACCOUNT_DEACTIVATED = "ACCOUNT_DEACTIVATED";
        public const string NOT_AUTHENTICATED = "NOT_AUTHENTICATED";
        public const string LOGIN_SUCCESS = "LOGIN_SUCCESS";
        public const string REGISTER_SUCCESS = "REGISTER_SUCCESS";
        public const string LOGOUT_SUCCESS = "LOGOUT_SUCCESS";
        public const string TOKEN_REFRESHED = "TOKEN_REFRESHED";
        public const string CURRENT_PASSWORD_INCORRECT = "CURRENT_PASSWORD_INCORRECT";
        public const string PASSWORD_TOO_WEAK = "PASSWORD_TOO_WEAK";
        public const string PASSWORD_CHANGED = "PASSWORD_CHANGED";
    }

    public static class User
    {
        public const string NOT_FOUND = "USER_NOT_FOUND";
        public const string EMAIL_EXISTS = "EMAIL_EXISTS";
        public const string USERNAME_EXISTS = "USERNAME_EXISTS";
        public const string CREATED = "USER_CREATED";
        public const string UPDATED = "USER_UPDATED";
        public const string DELETED = "USER_DELETED";
        public const string ACTIVATED = "USER_ACTIVATED";
        public const string DEACTIVATED = "USER_DEACTIVATED";
        public const string ROLES_ASSIGNED = "ROLES_ASSIGNED";
        public const string CREATION_FAILED = "USER_CREATION_FAILED";
        public const string UPDATE_FAILED = "USER_UPDATE_FAILED";
        public const string DELETE_FAILED = "USER_DELETE_FAILED";
        public const string ACTIVATE_FAILED = "ACTIVATE_FAILED";
        public const string DEACTIVATE_FAILED = "DEACTIVATE_FAILED";
        public const string REMOVE_ROLES_FAILED = "REMOVE_ROLES_FAILED";
        public const string ADD_ROLES_FAILED = "ADD_ROLES_FAILED";
    }

    public static class Content
    {
        public const string NOT_FOUND = "CONTENT_NOT_FOUND";
        public const string ALREADY_EXISTS = "CONTENT_EXISTS";
        public const string CREATED = "CONTENT_CREATED";
        public const string UPDATED = "CONTENT_UPDATED";
        public const string DELETED = "CONTENT_DELETED";
        public const string PUBLISHED = "CONTENT_PUBLISHED";
        public const string ARCHIVED = "CONTENT_ARCHIVED";

        /// <summary>FEAT-11, AC-167. A publish attempted from a status other than Draft.</summary>
        public const string NOT_PUBLISHABLE = "CONTENT_NOT_PUBLISHABLE";

        /// <summary>FEAT-11. An archive attempted from Archived (BR: "any status except Archived").</summary>
        public const string NOT_ARCHIVABLE = "CONTENT_NOT_ARCHIVABLE";
    }

    /// <summary>FEAT-11, AC-171, AC-172.</summary>
    public static class ContentCategory
    {
        public const string NOT_FOUND = "CONTENT_CATEGORY_NOT_FOUND";
        public const string NAME_EXISTS = "CONTENT_CATEGORY_NAME_EXISTS";
    }

    /// <summary>FEAT-11, AC-181.</summary>
    public static class ContentTicketLink
    {
        public const string EXISTS = "CONTENT_TICKET_LINK_EXISTS";
        public const string NOT_FOUND = "CONTENT_TICKET_LINK_NOT_FOUND";
    }

    public static class Notification
    {
        public const string NOT_FOUND = "NOTIFICATION_NOT_FOUND";
        public const string ACCESS_DENIED = "ACCESS_DENIED";
        public const string CREATED = "NOTIFICATION_CREATED";
        public const string MARKED_READ = "NOTIFICATION_MARKED_READ";
        public const string DELETED = "NOTIFICATION_DELETED";

        // FEAT-15 / notification gateway.
        public const string CONFIG_MISSING = "NOTIFICATION_CONFIG_MISSING";
        public const string TEMPLATE_INVALID = "NOTIFICATION_TEMPLATE_INVALID";
        public const string DELIVERY_FAILED = "NOTIFICATION_DELIVERY_FAILED";
        public const string INAPP_REQUIRES_USER = "NOTIFICATION_INAPP_REQUIRES_USER";
        public const string CHANNEL_NOT_SUPPORTED = "NOTIFICATION_CHANNEL_NOT_SUPPORTED";
        public const string SIGNAL_FAILED = "NOTIFICATION_SIGNALR_FAILED";
    }

    public static class PlatformSetting
    {
        public const string NOT_FOUND = "SETTING_NOT_FOUND";
        public const string ALREADY_EXISTS = "SETTING_EXISTS";
        public const string CREATED = "SETTING_CREATED";
        public const string UPDATED = "SETTING_UPDATED";
        public const string DELETED = "SETTING_DELETED";
        public const string REPROTECT_FAILED = "SETTING_REPROTECT_FAILED";
    }

    public static class ExternalApi
    {
        public const string NOT_CONFIGURED = "EXTERNAL_API_NOT_CONFIGURED";
        public const string ERROR = "EXTERNAL_API_ERROR";
        public const string NOT_FOUND = "EXTERNAL_API_CONFIG_NOT_FOUND";
        public const string ALREADY_EXISTS = "EXTERNAL_API_CONFIG_EXISTS";
    }

    public static class General
    {
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";
        public const string UNAUTHORIZED = "UNAUTHORIZED_ACCESS";
        public const string FORBIDDEN = "FORBIDDEN_ACCESS";
        public const string BAD_REQUEST = "BAD_REQUEST";
        public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";
        public const string SUCCESS_CREATED = "SUCCESS_CREATED";
        public const string AI_NOT_CONFIGURED = "AI_NOT_CONFIGURED";
        public const string AI_UNGROUNDED = "AI_UNGROUNDED";
        public const string AI_THREAD_TOO_SHORT = "AI_THREAD_TOO_SHORT";

        /// <summary>AI-32 — the resilient provider chain exhausted every provider.</summary>
        public const string AI_PROVIDER_FAILED = "AI_PROVIDER_FAILED";

        /// <summary>AI-32 — the last failure in the chain was a provider rate limit.</summary>
        public const string AI_RATE_LIMITED = "AI_RATE_LIMITED";

        /// <summary>AI-40 � a chat session that is unknown, another actor's, or another scope's.</summary>
        public const string AI_CHAT_NOT_FOUND = "AI_CHAT_NOT_FOUND";
        public const string SUCCESS_UPDATED = "SUCCESS_UPDATED";
        public const string SUCCESS_DELETED = "SUCCESS_DELETED";
        public const string SUCCESS_OPERATION = "SUCCESS_OPERATION";
    }

    public static class Permission
    {
        public const string NOT_FOUND = "PERMISSION_NOT_FOUND";
        public const string ROLE_NOT_FOUND = "PERMISSION_ROLE_NOT_FOUND";
        public const string MAPPING_NOT_FOUND = "PERMISSION_MAPPING_NOT_FOUND";
        public const string ASSIGNED = "PERMISSION_ASSIGNED";
        public const string REVOKED = "PERMISSION_REVOKED";
        public const string LAST_REQUIRED = "PERMISSION_LAST_REQUIRED";
        public const string UPDATED = "PERMISSION_UPDATED";
        public const string STALE_SNAPSHOT = "PERMISSION_STALE_SNAPSHOT";
    }

    /// <summary>Customer records — FEAT-03, AC-7…AC-16.</summary>
    public static class Customer
    {
        public const string NOT_FOUND = "CUSTOMER_NOT_FOUND";

        /// <summary>AC-9. A conflict, never a validation failure: the request is well formed.</summary>
        public const string EMAIL_EXISTS = "CUSTOMER_EMAIL_EXISTS";

        /// <summary>AC-15. Support history must not be destroyable by a single click.</summary>
        public const string HAS_TICKETS = "CUSTOMER_HAS_TICKETS";

        public const string CREATED = "CUSTOMER_CREATED";
        public const string UPDATED = "CUSTOMER_UPDATED";
        public const string DELETED = "CUSTOMER_DELETED";

        /// <summary>AC-75. An interaction record was appended; notes are never edited or removed.</summary>
        public const string NOTE_ADDED = "CUSTOMER_NOTE_ADDED";
    }

    /// <summary>Customer attachments — MVP-06, AC-22…AC-28.</summary>
    public static class Attachment
    {
        public const string NOT_FOUND = "ATTACHMENT_NOT_FOUND";

        /// <summary>AC-23. Answered 413: the request is well formed and simply too big.</summary>
        public const string TOO_LARGE = "ATTACHMENT_TOO_LARGE";

        /// <summary>AC-24. Answered 415, and refused for not being on the allowlist — not for
        /// having been recognised as dangerous.</summary>
        public const string TYPE_NOT_ALLOWED = "ATTACHMENT_TYPE_NOT_ALLOWED";

        /// <summary>
        /// A zero-byte upload. Not in the spec, but <c>Asset.Create</c> throws on it, so without a
        /// code here an empty file picked by accident would be a 500 instead of a field-keyed 400.
        /// </summary>
        public const string EMPTY = "ATTACHMENT_EMPTY";

        public const string ADDED = "ATTACHMENT_ADDED";
        public const string REMOVED = "ATTACHMENT_REMOVED";
    }

    /// <summary>The ticket workflow — FEAT-04, FEAT-05.</summary>
    public static class Ticket
    {
        public const string NOT_FOUND = "TICKET_NOT_FOUND";
        public const string CREATED = "TICKET_CREATED";

        /// <summary>
        /// AC-31. A customer referenced in the request BODY that does not exist is a field-keyed
        /// 400, not a 404 — the addressed resource (the ticket collection) does exist.
        /// </summary>
        public const string CUSTOMER_NOT_FOUND = "TICKET_CUSTOMER_NOT_FOUND";

        public const string CATEGORY_NOT_FOUND = "TICKET_CATEGORY_NOT_FOUND";

        /// <summary>AC-38. A conflict: the request is well formed and the state is wrong.</summary>
        public const string TRANSITION_NOT_ALLOWED = "TICKET_TRANSITION_NOT_ALLOWED";

        /// <summary>AC-39. The diagonal of the transition table is empty (BR-4).</summary>
        public const string ALREADY_IN_STATUS = "TICKET_ALREADY_IN_STATUS";

        /// <summary>AC-41. Optimistic concurrency: the first change stands, the second is refused.</summary>
        public const string MODIFIED_BY_ANOTHER_USER = "TICKET_MODIFIED_BY_ANOTHER_USER";

        /// <summary>US-923 / AC-923.2 — reclassification applied, priority re-derived.</summary>
        public const string RECLASSIFIED = "TICKET_RECLASSIFIED";

        // US-924.
        public const string TAG_ADDED = "TICKET_TAG_ADDED";
        public const string TAG_REMOVED = "TICKET_TAG_REMOVED";
        public const string TAG_NOT_FOUND = "TICKET_TAG_NOT_FOUND";

        // US-925.
        public const string LINK_TARGET_NOT_FOUND = "TICKET_LINK_TARGET_NOT_FOUND";
        public const string LINK_SELF = "TICKET_LINK_SELF";
        public const string LINK_EXISTS = "TICKET_LINK_EXISTS";
        public const string LINK_CYCLE = "TICKET_LINK_CYCLE";
        public const string LINK_NOT_FOUND = "TICKET_LINK_NOT_FOUND";
        public const string LINK_CREATED = "TICKET_LINK_CREATED";
        public const string LINK_REMOVED = "TICKET_LINK_REMOVED";

        /// <summary>AC-925.3. Resolving as Duplicate without a DuplicateOf link is a state conflict.</summary>
        public const string DUPLICATE_REQUIRES_LINK = "TICKET_DUPLICATE_REQUIRES_LINK";

        /// <summary>AC-45. Per-record authorization, decided only once the ticket is loaded.</summary>
        public const string NOT_ASSIGNED_TO_YOU = "TICKET_NOT_ASSIGNED_TO_YOU";

        /// <summary>AC-44. The target must exist and must actually be an agent.</summary>
        public const string ASSIGNEE_NOT_FOUND = "TICKET_ASSIGNEE_NOT_FOUND";
        public const string ASSIGNEE_NOT_AN_AGENT = "TICKET_ASSIGNEE_NOT_AN_AGENT";

        /// <summary>
        /// MVP-02 criterion 4. Distinct from <see cref="ASSIGNEE_NOT_AN_AGENT"/> on purpose: a
        /// deactivated agent *is* an agent, and "they have left" is a different thing for a
        /// supervisor to read than "that person was never in this role".
        /// </summary>
        public const string ASSIGNEE_DEACTIVATED = "TICKET_ASSIGNEE_DEACTIVATED";

        public const string STATUS_CHANGED = "TICKET_STATUS_CHANGED";
        public const string ASSIGNED = "TICKET_ASSIGNED";

        /// <summary>AC-101.</summary>
        public const string MESSAGE_RECORDED = "TICKET_MESSAGE_RECORDED";

        /// <summary>US-903 AC2 / AC-533. An agent may not assign to another agent.</summary>
        public const string ASSIGNMENT_REFUSED = "TICKET_ASSIGNMENT_REFUSED";

        /// <summary>US-904 / AC-506. Escalation owner set.</summary>
        public const string ESCALATION_OWNER_SET = "TICKET_ESCALATION_OWNER_SET";
    }

    /// <summary>FEAT-16, AC-115, AC-117..AC-120.</summary>
    public static class Department
    {
        public const string NOT_FOUND = "DEPARTMENT_NOT_FOUND";
        public const string CREATED = "DEPARTMENT_CREATED";
        public const string UPDATED = "DEPARTMENT_UPDATED";
        public const string DEACTIVATED = "DEPARTMENT_DEACTIVATED";
        public const string NAME_EXISTS = "DEPARTMENT_NAME_EXISTS";
    }

    /// <summary>FEAT-16, AC-116, AC-117, AC-120, AC-123.</summary>
    public static class Branch
    {
        public const string NOT_FOUND = "BRANCH_NOT_FOUND";
        public const string CREATED = "BRANCH_CREATED";
        public const string UPDATED = "BRANCH_UPDATED";
        public const string DEACTIVATED = "BRANCH_DEACTIVATED";
        public const string NAME_EXISTS = "BRANCH_NAME_EXISTS";
    }

    /// <summary>US-905, AC-508, AC-509.</summary>
    public static class Team
    {
        public const string NOT_FOUND = "TEAM_NOT_FOUND";
        public const string CREATED = "TEAM_CREATED";
        public const string UPDATED = "TEAM_UPDATED";
        public const string DEACTIVATED = "TEAM_DEACTIVATED";
        public const string NAME_EXISTS = "TEAM_NAME_EXISTS";
    }

    /// <summary>FEAT-17, AC-124..AC-127.</summary>
    public static class SLA
    {
        public const string POLICY_CREATED = "SLA_POLICY_CREATED";
        public const string POLICY_NOT_FOUND = "SLA_POLICY_NOT_FOUND";
        public const string POLICY_UPDATED = "SLA_POLICY_UPDATED";
        public const string POLICY_DEACTIVATED = "SLA_POLICY_DEACTIVATED";
    }

    /// <summary>US-215, AC-228 — business-hours calendar and public-holiday CRUD.</summary>
    public static class BusinessHours
    {
        public const string CALENDAR_CREATED = "BUSINESS_HOURS_CALENDAR_CREATED";
        public const string HOLIDAY_CREATED = "BUSINESS_HOURS_HOLIDAY_CREATED";
    }

    public static class Validation
    {
        public const string REQUIRED_FIELD = "REQUIRED_FIELD";

        // Customers — FEAT-03.
        public const string NAME_REQUIRED = "NAME_REQUIRED";
        public const string NAME_MAX_LENGTH = "NAME_MAX_LENGTH";
        public const string EMAIL_MAX_LENGTH = "EMAIL_MAX_LENGTH";
        public const string PHONE_MAX_LENGTH = "PHONE_MAX_LENGTH";

        // Customer notes — MVP-05, AC-75. Distinct from the content BODY_REQUIRED above so the
        // two surfaces can word their messages independently.
        public const string NOTE_BODY_REQUIRED = "NOTE_BODY_REQUIRED";
        public const string NOTE_BODY_MAX_LENGTH = "NOTE_BODY_MAX_LENGTH";

        // Paging — AC-11, applied to every paged read.
        public const string PAGE_SIZE_EXCEEDED = "PAGE_SIZE_EXCEEDED";

        // Tickets — FEAT-04, FEAT-05.
        public const string SUBJECT_REQUIRED = "SUBJECT_REQUIRED";
        public const string SUBJECT_MAX_LENGTH = "SUBJECT_MAX_LENGTH";
        public const string DESCRIPTION_REQUIRED = "DESCRIPTION_REQUIRED";
        public const string TICKET_PRIORITY_REQUIRED = "TICKET_PRIORITY_REQUIRED";
        public const string TICKET_PRIORITY_INVALID = "TICKET_PRIORITY_INVALID";
        public const string TICKET_STATUS_INVALID = "TICKET_STATUS_INVALID";
        public const string TICKET_SOURCE_INVALID = "TICKET_SOURCE_INVALID";

        // US-922 — resolution discipline (AC-922.1/3).
        public const string RESOLUTION_CODE_REQUIRED = "RESOLUTION_CODE_REQUIRED";
        public const string RESOLUTION_CODE_INVALID = "RESOLUTION_CODE_INVALID";
        public const string RESOLUTION_NOTES_REQUIRED = "RESOLUTION_NOTES_REQUIRED";
        public const string RESOLUTION_NOTES_MAX_LENGTH = "RESOLUTION_NOTES_MAX_LENGTH";

        // US-923 — impact/urgency classification (AC-923.1).
        public const string TICKET_IMPACT_REQUIRED = "TICKET_IMPACT_REQUIRED";
        public const string TICKET_IMPACT_INVALID = "TICKET_IMPACT_INVALID";
        public const string TICKET_URGENCY_REQUIRED = "TICKET_URGENCY_REQUIRED";
        public const string TICKET_URGENCY_INVALID = "TICKET_URGENCY_INVALID";

        // US-924 — tags (AC-924.1).
        public const string TICKET_TAG_INVALID = "TICKET_TAG_INVALID";
        public const string TICKET_TAG_DUPLICATE = "TICKET_TAG_DUPLICATE";
        public const string TICKET_TAG_LIMIT = "TICKET_TAG_LIMIT";

        // US-925 — links (AC-925.1).
        public const string TICKET_LINK_TYPE_INVALID = "TICKET_LINK_TYPE_INVALID";
        public const string TICKET_LINK_TARGET_REQUIRED = "TICKET_LINK_TARGET_REQUIRED";

        // FEAT-34 / AC-806.6 — the batch permission set's field-keyed refusals.
        public const string PERMISSION_SET_INVALID = "PERMISSION_SET_INVALID";
        public const string PERMISSION_SNAPSHOT_REQUIRED = "PERMISSION_SNAPSHOT_REQUIRED";

        // Ticket messages — FEAT-14, AC-101..AC-104.
        public const string MESSAGE_BODY_REQUIRED = "MESSAGE_BODY_REQUIRED";
        public const string MESSAGE_BODY_MAX_LENGTH = "MESSAGE_BODY_MAX_LENGTH";
        public const string MESSAGE_SUBJECT_MAX_LENGTH = "MESSAGE_SUBJECT_MAX_LENGTH";
        public const string MESSAGE_DIRECTION_INVALID = "MESSAGE_DIRECTION_INVALID";
        public const string MESSAGE_CHANNEL_INVALID = "MESSAGE_CHANNEL_INVALID";

        // Inbound external channels — FEAT-24..FEAT-27 (CC-1..CC-29).
        public const string CHANNEL_CONTACT_REQUIRED = "CHANNEL_CONTACT_REQUIRED";

        // Organisation structure — FEAT-16, AC-121.
        public const string ORG_NAME_REQUIRED = "ORG_NAME_REQUIRED";
        public const string ORG_NAME_MAX_LENGTH = "ORG_NAME_MAX_LENGTH";
        public const string ORG_TIMEZONE_MAX_LENGTH = "ORG_TIMEZONE_MAX_LENGTH";
        public const string DEPARTMENT_ID_REQUIRED = "DEPARTMENT_ID_REQUIRED";

        // Reports — FEAT-19+, AC-154.
        public const string REPORT_RANGE_INVALID = "REPORT_RANGE_INVALID";
        public const string REPORT_GROUP_BY_INVALID = "REPORT_GROUP_BY_INVALID";

        // SLA policies — FEAT-17, AC-126.
        public const string SLA_PRIORITY_INVALID = "SLA_PRIORITY_INVALID";
        public const string SLA_RESPONSE_TARGET_INVALID = "SLA_RESPONSE_TARGET_INVALID";
        public const string SLA_RESOLUTION_TARGET_INVALID = "SLA_RESOLUTION_TARGET_INVALID";
        public const string CUSTOMER_ID_REQUIRED = "CUSTOMER_ID_REQUIRED";
        public const string CATEGORY_ID_REQUIRED = "CATEGORY_ID_REQUIRED";
        public const string STATUS_REQUIRED_FIELD = "TICKET_STATUS_REQUIRED";
        public const string ROW_VERSION_REQUIRED = "ROW_VERSION_REQUIRED";
        public const string ASSIGNEE_ID_REQUIRED = "ASSIGNEE_ID_REQUIRED";
        public const string INVALID_EMAIL = "INVALID_EMAIL";
        public const string INVALID_PHONE = "INVALID_PHONE";
        public const string MIN_LENGTH = "MIN_LENGTH";
        public const string MAX_LENGTH = "MAX_LENGTH";
        public const string INVALID_FORMAT = "INVALID_FORMAT";
        public const string EMAIL_REQUIRED = "EMAIL_REQUIRED";
        public const string PASSWORD_REQUIRED = "PASSWORD_REQUIRED";
        public const string USERNAME_REQUIRED = "USERNAME_REQUIRED";
        public const string FIRST_NAME_REQUIRED = "FIRST_NAME_REQUIRED";
        public const string LAST_NAME_REQUIRED = "LAST_NAME_REQUIRED";
        public const string TOKEN_REQUIRED = "TOKEN_REQUIRED";
        public const string TITLE_REQUIRED = "TITLE_REQUIRED";
        public const string TITLE_MAX_LENGTH = "TITLE_MAX_LENGTH";
        public const string BODY_REQUIRED = "BODY_REQUIRED";
        public const string SUMMARY_MAX_LENGTH = "SUMMARY_MAX_LENGTH";
        public const string CONTENT_TYPE_REQUIRED = "CONTENT_TYPE_REQUIRED";
        public const string CONTENT_TYPE_MAX_LENGTH = "CONTENT_TYPE_MAX_LENGTH";
        public const string AUTHOR_ID_REQUIRED = "AUTHOR_ID_REQUIRED";
        public const string STATUS_REQUIRED = "STATUS_REQUIRED";
        public const string STATUS_INVALID = "STATUS_INVALID";
        public const string FEATURED_IMAGE_URL_MAX_LENGTH = "FEATURED_IMAGE_URL_MAX_LENGTH";
        public const string CATEGORY_MAX_LENGTH = "CATEGORY_MAX_LENGTH";
        public const string USER_ID_REQUIRED = "USER_ID_REQUIRED";
        public const string MESSAGE_REQUIRED = "MESSAGE_REQUIRED";
        public const string MESSAGE_MAX_LENGTH = "MESSAGE_MAX_LENGTH";
        public const string NOTIFICATION_TYPE_REQUIRED = "NOTIFICATION_TYPE_REQUIRED";
        public const string NOTIFICATION_TYPE_MAX_LENGTH = "NOTIFICATION_TYPE_MAX_LENGTH";
        public const string CHANNEL_REQUIRED = "CHANNEL_REQUIRED";
        public const string CHANNEL_INVALID = "CHANNEL_INVALID";
        public const string KEY_REQUIRED = "KEY_REQUIRED";
        public const string KEY_MAX_LENGTH = "KEY_MAX_LENGTH";
        public const string VALUE_REQUIRED = "VALUE_REQUIRED";
        public const string VALUE_MAX_LENGTH = "VALUE_MAX_LENGTH";
        public const string PASSWORD_UPPERCASE = "PASSWORD_UPPERCASE";
        public const string PASSWORD_LOWERCASE = "PASSWORD_LOWERCASE";
        public const string PASSWORD_NUMBER = "PASSWORD_NUMBER";
    }

    /// <summary>Inbound external channel webhooks — FEAT-24..FEAT-27 (CC-27).</summary>
    public static class Channel
    {
        /// <summary>CC-5/CC-27. The provider's signature on a webhook did not verify; the payload
        /// is refused before any database work and before it is logged.</summary>
        public const string WEBHOOK_SIGNATURE_INVALID = "CHANNEL_WEBHOOK_SIGNATURE_INVALID";

        /// <summary>The webhook body did not parse into a supported provider payload shape.</summary>
        public const string PAYLOAD_INVALID = "CHANNEL_PAYLOAD_INVALID";
    }

    /// <summary>OTP contact verification — profile update flow (AC-439..AC-445).</summary>
    public static class Verification
    {
        /// <summary>Every unusable state — wrong, malformed, expired, invalidated, locked, unknown id,
        /// or a record belonging to another user — collapses to this single safe code so the response
        /// never reveals which condition occurred (AC-440, AC-443).</summary>
        public const string INVALID = "OTP_INVALID";

        /// <summary>A contact was confirmed successfully.</summary>
        public const string VERIFIED = "OTP_VERIFIED";

        /// <summary>A fresh code was dispatched and a verification record created (OTP-1, OTP-2).</summary>
        public const string REQUESTED = "OTP_REQUESTED";

        /// <summary>A request for the same contact and channel arrived inside the 60-second cooldown (OTP-3).</summary>
        public const string COOLDOWN = "OTP_COOLDOWN";

        /// <summary>The code could not be delivered; nothing was persisted (OTP-9).</summary>
        public const string DISPATCH_FAILED = "OTP_DISPATCH_FAILED";
    }

    /// <summary>Customer satisfaction survey — FEAT-22 portal (US-408/US-409, PJ-11/12).</summary>
    public static class Survey
    {
        /// <summary>A ticket already has a survey response; the submit is refused (PJ-11).</summary>
        public const string ALREADY_SUBMITTED = "SURVEY_ALREADY_SUBMITTED";

        /// <summary>The ticket is not yet resolved/closed, so no survey may be given (A8).</summary>
        public const string TICKET_NOT_RESOLVED = "SURVEY_TICKET_NOT_RESOLVED";

        /// <summary>The response was recorded.</summary>
        public const string SUBMITTED = "SURVEY_SUBMITTED";

        public const string RATING_REQUIRED = "SURVEY_RATING_REQUIRED";
        public const string RATING_INVALID = "SURVEY_RATING_INVALID";
    }
}

