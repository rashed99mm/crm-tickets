namespace CustomerSupport.Shared.Contracts;

public static class Topics
{
    public static string NotificationMessages => "notification.messages";
    public static string SmsMessages => "sms.messages";
    public static string EmailMessages => "email.messages";
    public static string UserLoggedIn => "user.loggedin";
    public static string UserLoggedOut => "user.loggedout";
    public static string SlaEscalated => "sla.messages.escalated";
    public static string ChatMessagesPushed => "chat.messages.pushed";
}
