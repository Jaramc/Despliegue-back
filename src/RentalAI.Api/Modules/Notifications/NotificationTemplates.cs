using System.Globalization;

namespace RentalAI.Api.Modules.Notifications;

public static class NotificationTemplates
{
    private const string Accent = "#DC97E9";

    public static EmailContent BookingConfirmed(string guestEmail, string propertyTitle, DateTime checkIn, DateTime checkOut, decimal totalPrice)
    {
        var body = Layout("Reserva confirmada", $"""
            <p>Tu reserva en <strong>{propertyTitle}</strong> está confirmada.</p>
            <table style="margin:16px 0;border-collapse:collapse;">
                <tr><td style="padding:4px 12px 4px 0;color:#666;">Entrada</td><td style="padding:4px 0;">{FormatDate(checkIn)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#666;">Salida</td><td style="padding:4px 0;">{FormatDate(checkOut)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#666;">Total</td><td style="padding:4px 0;"><strong>{FormatPrice(totalPrice)}</strong></td></tr>
            </table>
            <p>¡Buen viaje!</p>
            """);
        return new EmailContent("Tu reserva está confirmada", body);
    }

    public static EmailContent KycApproved(string userEmail)
    {
        var body = Layout("Verificación aprobada", """
            <p>Tu verificación de identidad fue aprobada.</p>
            <p>Ya puedes confirmar reservas en RentalAI.</p>
            """);
        return new EmailContent("Verificación de identidad aprobada", body);
    }

    public static EmailContent KycRejected(string userEmail, string reason)
    {
        var body = Layout("Verificación rechazada", $"""
            <p>Tu verificación de identidad fue rechazada.</p>
            <p style="color:#666;">Motivo: {reason}</p>
            <p>Puedes intentarlo de nuevo con un documento legible.</p>
            """);
        return new EmailContent("Verificación de identidad rechazada", body);
    }

    public static EmailContent CheckInReminder(string guestEmail, string propertyTitle, DateTime checkIn)
    {
        var body = Layout("Tu entrada se acerca", $"""
            <p>Tu estancia en <strong>{propertyTitle}</strong> comienza pronto.</p>
            <p>Entrada: <strong>{FormatDate(checkIn)}</strong> a las 14:00.</p>
            """);
        return new EmailContent("Recordatorio de entrada", body);
    }

    public static EmailContent CheckOutReminder(string guestEmail, string propertyTitle, DateTime checkOut)
    {
        var body = Layout("Tu salida se acerca", $"""
            <p>Tu estancia en <strong>{propertyTitle}</strong> termina pronto.</p>
            <p>Salida: <strong>{FormatDate(checkOut)}</strong> a las 12:00.</p>
            """);
        return new EmailContent("Recordatorio de salida", body);
    }

    private static string Layout(string heading, string content) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;background:#f5f5f7;padding:24px;">
            <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;">
                <div style="background:{Accent};padding:20px 28px;">
                    <span style="color:#ffffff;font-size:22px;font-weight:700;letter-spacing:0.5px;">RentalAI</span>
                </div>
                <div style="padding:28px;color:#222;font-size:15px;line-height:1.5;">
                    <h1 style="margin:0 0 16px;font-size:20px;color:{Accent};">{heading}</h1>
                    {content}
                </div>
                <div style="padding:18px 28px;border-top:1px solid #eee;color:#999;font-size:12px;">
                    Este es un correo automático de RentalAI.
                </div>
            </div>
        </div>
        """;

    private static string FormatDate(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string FormatPrice(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
}
