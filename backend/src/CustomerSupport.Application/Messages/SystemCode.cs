namespace CustomerSupport.Application.Messages;

/// <summary>
/// Unique system codes for every response. CON = Confirmation (success), VAL = Validation,
/// ERR = Error. Each domain key in Resources.yaml maps to exactly one code.
/// </summary>
public static class SystemCode
{
    // ── Success (CON) ──────────────────────────────────────────────────────
    public const string CON001 = "CON001"; // Login success
    public const string CON002 = "CON002"; // Register success
    public const string CON003 = "CON003"; // Logout success
    public const string CON004 = "CON004"; // Token refreshed
    public const string CON005 = "CON005"; // Password changed
    public const string CON006 = "CON006"; // User created
    public const string CON007 = "CON007"; // User updated
    public const string CON008 = "CON008"; // User deleted
    public const string CON009 = "CON009"; // User activated
    public const string CON010 = "CON010"; // User deactivated
    public const string CON011 = "CON011"; // Roles assigned
    public const string CON012 = "CON012"; // Content created
    public const string CON013 = "CON013"; // Content updated
    public const string CON014 = "CON014"; // Content deleted
    public const string CON015 = "CON015"; // Content published
    public const string CON016 = "CON016"; // Content archived
    public const string CON017 = "CON017"; // Notification created
    public const string CON018 = "CON018"; // Notification marked read
    public const string CON019 = "CON019"; // Notification deleted
    public const string CON020 = "CON020"; // Setting created
    public const string CON021 = "CON021"; // Setting updated
    public const string CON022 = "CON022"; // Setting deleted
    public const string CON023 = "CON023"; // Customer created
    public const string CON024 = "CON024"; // Customer updated
    public const string CON025 = "CON025"; // Customer deleted
    public const string CON026 = "CON026"; // Customer note added
    public const string CON027 = "CON027"; // Attachment added
    public const string CON028 = "CON028"; // Attachment removed
    public const string CON029 = "CON029"; // Ticket created
    public const string CON030 = "CON030"; // Ticket status changed
    public const string CON031 = "CON031"; // Ticket assigned
    public const string CON032 = "CON032"; // Generic success created
    public const string CON033 = "CON033"; // Generic success updated
    public const string CON034 = "CON034"; // Generic success deleted
    public const string CON035 = "CON035"; // Generic success operation
    public const string CON036 = "CON036"; // Department created
    public const string CON037 = "CON037"; // Department updated
    public const string CON038 = "CON038"; // Department deactivated
    public const string CON039 = "CON039"; // Branch created
    public const string CON040 = "CON040"; // Branch updated
    public const string CON041 = "CON041"; // Branch deactivated
    public const string CON042 = "CON042"; // SLA policy created
    public const string CON043 = "CON043"; // SLA policy updated
    public const string CON044 = "CON044"; // SLA policy deactivated
    public const string CON045 = "CON045"; // Business hours calendar created
    public const string CON046 = "CON046"; // Public holiday created

    // ── Validation (VAL) ──────────────────────────────────────────────────
    public const string VAL001 = "VAL001"; // General validation error
    public const string VAL002 = "VAL002"; // Required field
    public const string VAL003 = "VAL003"; // Invalid email
    public const string VAL004 = "VAL004"; // Invalid phone
    public const string VAL005 = "VAL005"; // Min length
    public const string VAL006 = "VAL006"; // Max length
    public const string VAL007 = "VAL007"; // Invalid format
    public const string VAL008 = "VAL008"; // Password uppercase
    public const string VAL009 = "VAL009"; // Password lowercase
    public const string VAL010 = "VAL010"; // Password number
    public const string VAL011 = "VAL011"; // Page size exceeded
    public const string VAL012 = "VAL012"; // Name required
    public const string VAL013 = "VAL013"; // Name max length
    public const string VAL014 = "VAL014"; // Email max length
    public const string VAL015 = "VAL015"; // Phone max length
    public const string VAL016 = "VAL016"; // Email required
    public const string VAL017 = "VAL017"; // Password required
    public const string VAL018 = "VAL018"; // Username required
    public const string VAL019 = "VAL019"; // First name required
    public const string VAL020 = "VAL020"; // Last name required
    public const string VAL021 = "VAL021"; // Token required
    public const string VAL022 = "VAL022"; // Title required
    public const string VAL023 = "VAL023"; // Title max length
    public const string VAL024 = "VAL024"; // Body required
    public const string VAL025 = "VAL025"; // Summary max length
    public const string VAL026 = "VAL026"; // Content type required
    public const string VAL027 = "VAL027"; // Content type max length
    public const string VAL028 = "VAL028"; // Author ID required
    public const string VAL029 = "VAL029"; // Status required
    public const string VAL030 = "VAL030"; // Status invalid
    public const string VAL031 = "VAL031"; // Featured image URL max length
    public const string VAL032 = "VAL032"; // Category max length
    public const string VAL033 = "VAL033"; // User ID required
    public const string VAL034 = "VAL034"; // Message required
    public const string VAL035 = "VAL035"; // Message max length
    public const string VAL036 = "VAL036"; // Notification type required
    public const string VAL037 = "VAL037"; // Notification type max length
    public const string VAL038 = "VAL038"; // Channel required
    public const string VAL039 = "VAL039"; // Channel invalid
    public const string VAL040 = "VAL040"; // Key required
    public const string VAL041 = "VAL041"; // Key max length
    public const string VAL042 = "VAL042"; // Value required
    public const string VAL043 = "VAL043"; // Value max length
    public const string VAL044 = "VAL044"; // Subject required
    public const string VAL045 = "VAL045"; // Subject max length
    public const string VAL046 = "VAL046"; // Description required
    public const string VAL047 = "VAL047"; // Priority required
    public const string VAL048 = "VAL048"; // Priority invalid
    public const string VAL049 = "VAL049"; // Status invalid (ticket)
    public const string VAL050 = "VAL050"; // Customer ID required
    public const string VAL051 = "VAL051"; // Category ID required
    public const string VAL052 = "VAL052"; // Note body required
    public const string VAL053 = "VAL053"; // Note body max length
    public const string VAL054 = "VAL054"; // Row version required
    public const string VAL055 = "VAL055"; // Assignee ID required
    public const string VAL056 = "VAL056"; // Ticket status required
    public const string VAL057 = "VAL057"; // Organisation name required
    public const string VAL058 = "VAL058"; // Organisation name max length
    public const string VAL059 = "VAL059"; // Organisation timezone max length
    public const string VAL060 = "VAL060"; // SLA priority invalid
    public const string VAL061 = "VAL061"; // SLA response target invalid
    public const string VAL062 = "VAL062"; // SLA resolution target invalid
    public const string VAL063 = "VAL063"; // Inbound channel contact required

    // ── Error (ERR) ───────────────────────────────────────────────────────
    public const string ERR001 = "ERR001"; // Not found (generic)
    public const string ERR002 = "ERR002"; // Conflict (generic)
    public const string ERR003 = "ERR003"; // Unauthorized
    public const string ERR004 = "ERR004"; // Forbidden
    public const string ERR005 = "ERR005"; // Internal error
    public const string ERR006 = "ERR006"; // Bad request
    public const string ERR007 = "ERR007"; // Customer not found
    public const string ERR008 = "ERR008"; // Customer email exists
    public const string ERR009 = "ERR009"; // Customer has tickets
    public const string ERR010 = "ERR010"; // Ticket not found
    public const string ERR011 = "ERR011"; // Ticket customer not found
    public const string ERR012 = "ERR012"; // Ticket category not found
    public const string ERR013 = "ERR013"; // Ticket transition not allowed
    public const string ERR014 = "ERR014"; // Ticket already in status
    public const string ERR015 = "ERR015"; // Ticket modified by another user
    public const string ERR016 = "ERR016"; // Ticket not assigned to you
    public const string ERR017 = "ERR017"; // Ticket assignee not found
    public const string ERR018 = "ERR018"; // Ticket assignee not an agent
    public const string ERR019 = "ERR019"; // Ticket assignee deactivated
    public const string ERR020 = "ERR020"; // User not found
    public const string ERR021 = "ERR021"; // Email exists
    public const string ERR022 = "ERR022"; // Username exists
    public const string ERR023 = "ERR023"; // Invalid credentials
    public const string ERR024 = "ERR024"; // Invalid token
    public const string ERR025 = "ERR025"; // Invalid refresh token
    public const string ERR026 = "ERR026"; // Account deactivated
    public const string ERR027 = "ERR027"; // Not authenticated
    public const string ERR028 = "ERR028"; // Current password incorrect
    public const string ERR029 = "ERR029"; // Password too weak
    public const string ERR030 = "ERR030"; // Content not found
    public const string ERR031 = "ERR031"; // Content already exists
    public const string ERR032 = "ERR032"; // Notification not found
    public const string ERR033 = "ERR033"; // Notification access denied
    public const string ERR034 = "ERR034"; // Setting not found
    public const string ERR035 = "ERR035"; // Setting already exists
    public const string ERR036 = "ERR036"; // Setting reprotect failed
    public const string ERR037 = "ERR037"; // External API not configured
    public const string ERR038 = "ERR038"; // External API error
    public const string ERR039 = "ERR039"; // External API config not found
    public const string ERR040 = "ERR040"; // External API config exists
    public const string ERR041 = "ERR041"; // Attachment not found
    public const string ERR042 = "ERR042"; // Attachment too large
    public const string ERR043 = "ERR043"; // Attachment type not allowed
    public const string ERR044 = "ERR044"; // Attachment empty
    public const string ERR045 = "ERR045"; // Payload too large
    public const string ERR046 = "ERR046"; // Unsupported media type
    public const string ERR047 = "ERR047"; // Department not found
    public const string ERR048 = "ERR048"; // Branch not found
    public const string ERR049 = "ERR049"; // Department name exists
    public const string ERR050 = "ERR050"; // Branch name exists
    public const string ERR051 = "ERR051"; // SLA policy not found
    public const string ERR052 = "ERR052"; // AI assist not configured
    public const string ERR053 = "ERR053"; // AI answer not grounded in the knowledge base (QA001)
    public const string ERR054 = "ERR054"; // AI thread too short to summarise
    public const string ERR055 = "ERR055"; // Content not publishable from current status
    public const string ERR056 = "ERR056"; // Content not archivable from current status
    public const string ERR057 = "ERR057"; // Content category not found
    public const string ERR058 = "ERR058"; // Content category name exists
        public const string ERR059 = "ERR059"; // Content-ticket link already exists
        public const string ERR060 = "ERR060"; // Content-ticket link not found
        public const string ERR061 = "ERR061"; // Notification provider configuration missing
        public const string ERR062 = "ERR062"; // Notification template invalid / missing variable
        public const string ERR063 = "ERR063"; // Notification delivery failed
        public const string ERR064 = "ERR064"; // In-app notification requires a recipient user
        public const string ERR065 = "ERR065"; // Notification channel not supported
        public const string ERR066 = "ERR066"; // SignalR push failed
        public const string ERR067 = "ERR067"; // Channel webhook signature invalid
        public const string ERR068 = "ERR068"; // Channel webhook payload invalid
        public const string ERR069 = "ERR069"; // OTP verification failed (safe, single code)
        public const string ERR070 = "ERR070"; // AI provider chain failed
        public const string ERR071 = "ERR071"; // AI provider rate limited
        public const string ERR072 = "ERR072"; // AI chat session not found
        public const string ERR073 = "ERR073"; // OTP code could not be delivered
        public const string ERR074 = "ERR074"; // OTP resend cooldown active
        public const string ERR075 = "ERR075"; // Survey already submitted (duplicate)
        public const string ERR076 = "ERR076"; // Survey ticket not resolved
        public const string CON067 = "CON067"; // OTP contact verified
        public const string CON068 = "CON068"; // OTP code dispatched
        public const string CON069 = "CON069"; // Survey submitted

        public const string ERR077 = "ERR077"; // Team not found
        public const string ERR078 = "ERR078"; // Team name exists
        public const string ERR079 = "ERR079"; // Ticket assignment refused (AC-533)

        public const string CON070 = "CON070"; // Team created
        public const string CON071 = "CON071"; // Team updated
        public const string CON072 = "CON072"; // Team deactivated
        public const string CON073 = "CON073"; // Escalation owner set (AC-506)

        public const string VAL064 = "VAL064"; // Survey rating required
        public const string VAL065 = "VAL065"; // Survey rating invalid (1..5)
        public const string VAL066 = "VAL066"; // Department ID required

        public const string VAL067 = "VAL067"; // Resolution code required (AC-922.1)
        public const string VAL068 = "VAL068"; // Resolution code invalid (AC-922.3)
        public const string VAL069 = "VAL069"; // Resolution notes required (AC-922.1)
        public const string VAL070 = "VAL070"; // Resolution notes too long (AC-922.3)

        public const string VAL071 = "VAL071"; // Ticket impact required (AC-923.1)
        public const string VAL072 = "VAL072"; // Ticket impact invalid (AC-923.1)
        public const string VAL073 = "VAL073"; // Ticket urgency required (AC-923.1)
        public const string VAL074 = "VAL074"; // Ticket urgency invalid (AC-923.1)

        public const string CON074 = "CON074"; // Ticket reclassified (AC-923.2)

        public const string VAL075 = "VAL075"; // Tag invalid (AC-924.1)
        public const string VAL076 = "VAL076"; // Tag duplicate (AC-924.1)
        public const string VAL077 = "VAL077"; // Tag limit reached (AC-924.1)

        public const string ERR080 = "ERR080"; // Tag not found on ticket

        public const string CON075 = "CON075"; // Tag added
        public const string CON076 = "CON076"; // Tag removed

        public const string VAL078 = "VAL078"; // Link type invalid (AC-925.1)
        public const string VAL079 = "VAL079"; // Link target reference required (AC-925.1)

        public const string ERR081 = "ERR081"; // Link target ticket not found
        public const string ERR082 = "ERR082"; // Link already exists
        public const string ERR083 = "ERR083"; // Direct duplicate cycle (AC-925.2)
        public const string ERR084 = "ERR084"; // Self link
        public const string ERR085 = "ERR085"; // Duplicate resolution requires a DuplicateOf link (AC-925.3)
        public const string ERR086 = "ERR086"; // Link not found

        public const string CON077 = "CON077"; // Link created
        public const string CON078 = "CON078"; // Link removed

        // FEAT-34 — role permission workbench (AC-806.x). Last used before this feature:
        // CON078, ERR086, VAL079.
        public const string CON079 = "CON079"; // Role permission set updated (AC-806.1)
        public const string ERR087 = "ERR087"; // Role permission snapshot is stale (AC-806.5)
        public const string VAL080 = "VAL080"; // Permission set invalid (AC-806.6)
        public const string VAL081 = "VAL081"; // Expected permission snapshot required (AC-806.6)
    }
