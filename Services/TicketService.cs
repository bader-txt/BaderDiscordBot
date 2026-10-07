using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BaderDiscordBot.Services
{
    public class TicketService
    {
        private readonly DiscordSocketClient _client;

        public TicketService(DiscordSocketClient client)
        {
            _client = client;
            // تسجيل الاستجابة لضغطات الأزرار (Buttons)
            _client.ButtonExecuted += OnButtonExecutedAsync;
        }

        /// <summary>
        /// دالة لإرسال رسالة البانل الرئيسية للتيكتات مع الزر
        /// </summary>
        public async Task SendTicketPanelAsync(SocketTextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("⚡ مركز دعم جلسات التويك - BaderTweaker")
                .WithDescription("أهلاً بك! للحصول على جلسة تويك مخصصة لجهازك أو للاستفسار عن الخدمات، اضغط على الزر أدناه لفتح تيكت خاص بك.")
                .WithColor(new Color(0x38, 0xBD, 0xF8)) // لون أزرق سماوي
                .WithFooter("BaderTweaker Support System")
                .Build();

            var builder = new ComponentBuilder()
                .WithButton("📩 فتح تيكت تويك (Tweak Ticket)", "create_tweak_ticket", ButtonStyle.Primary, new Emoji("🎫"));

            await channel.SendMessageAsync(embed: embed, components: builder.Build());
        }

        private async Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            try
            {
                // 1️⃣ عند ضغط زر فتح التيكت
                if (component.Data.CustomId == "create_tweak_ticket")
                {
                    // ⚡ تأكيد الاستجابة فوراً لمنع خطأ "لم يستجب التطبيق في الوقت"
                    await component.DeferAsync(ephemeral: true);

                    var guild = (component.Channel as SocketGuildChannel)?.Guild;
                    var user = component.User as SocketGuildUser;

                    if (guild == null || user == null) return;

                    // تنظيف اسم العضو ليكون صالحاً كاسم قناة
                    string cleanUsername = user.Username.ToLower().Replace(" ", "-");
                    string channelName = $"tweak-{cleanUsername}";

                    // التأكد من عدم وجود تيكت مفتوح سابقاً لنفس العميل
                    var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
                    if (existingChannel != null)
                    {
                        await component.FollowupAsync($"❌ لديك تيكت مفتوح بالفعل هنا: {existingChannel.Mention}", ephemeral: true);
                        return;
                    }

                    // 📂 البحث عن فئة (Category) للتيكتات أو إنشاؤها تلقائياً إذا لم تكن موجودة
                    string categoryName = "🎫 | تيكتات التويك";
                    var category = guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    
                    if (category == null)
                    {
                        category = await guild.CreateCategoryAsync(categoryName);
                    }

                    // 📝 إنشاء القناة النصية ووضعها داخل الفئة المحددة
                    var ticketChannel = await guild.CreateTextChannelAsync(channelName, tcp =>
                    {
                        tcp.CategoryId = category.Id; // ربط القناة بالفئة
                        tcp.PermissionOverwrites = new[]
                        {
                            // إخفاء القناة عن باقي الأعضاء
                            new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                            // السماح للعميل برؤية القناة والإرسال فيها
                            new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(
                                viewChannel: PermValue.Allow,
                                sendMessages: PermValue.Allow,
                                attachFiles: PermValue.Allow,
                                readMessageHistory: PermValue.Allow))
                        };
                    });

                    // رسالة الترحيب بداخل روم التيكت الجديد
                    var welcomeEmbed = new EmbedBuilder()
                        .WithTitle($"🎫 أهلاً بك يا {user.Username} في تيكت التويك")
                        .WithDescription("يرجى كتابة تفاصيل مواصفات جهازك والمشكلة التي تواجهها، وسيقوم الدعم الفني بالرد عليك لبدء الجلسة.\n\nلإغلاق التيكت بعد الانتهاء، اضغط على الزر أدناه.")
                        .WithColor(Color.Green)
                        .Build();

                    var closeButton = new ComponentBuilder()
                        .WithButton("🔒 إغلاق التيكت", "close_ticket", ButtonStyle.Danger, new Emoji("🛑"));

                    await ticketChannel.SendMessageAsync(text: $"{user.Mention}", embed: welcomeEmbed, components: closeButton.Build());

                    // إرسال الرد النهائي للعميل برابط التيكت
                    await component.FollowupAsync($"✅ تم إنشاء التيكت الخاص بك بنجاح في فئة التيكتات: {ticketChannel.Mention}", ephemeral: true);
                }
                // 2️⃣ عند ضغط زر إغلاق التيكت
                else if (component.Data.CustomId == "close_ticket")
                {
                    await component.DeferAsync(ephemeral: true);
                    await component.FollowupAsync("🔒 سيتم حذف وإغلاق هذا التيكت خلال 5 ثوانٍ...", ephemeral: true);
                    await Task.Delay(5000);

                    if (component.Channel is SocketTextChannel channel)
                    {
                        await channel.DeleteAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TicketService Error]: {ex.Message}");
            }
        }
    }
}
