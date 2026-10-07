using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BaderDiscordBot.Services
{
    public class TicketService
    {
        private readonly DiscordSocketClient _client;

        // 📌 آيدي (ID) قناة التعليمات والطلبات المخصصة
        private const ulong InstructionsChannelId = 1557489097562652824;

        public TicketService(DiscordSocketClient client)
        {
            _client = client;
            _client.ButtonExecuted += OnButtonExecutedAsync;
        }

        /// <summary>
        /// إرسال لوحة فتح التيكتات الرئيسية
        /// </summary>
        public async Task SendTicketPanelAsync(SocketTextChannel channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle("⚡ مركز دعم جلسات التويك - BaderTweaker")
                .WithDescription("أهلاً بك! للحصول على جلسة تويك مخصصة لجهازك أو للاستفسار عن الخدمات، اضغط على الزر أدناه لفتح تيكت خاص بك.")
                .WithColor(new Color(0x38, 0xBD, 0xF8))
                .WithFooter("BaderTweaker Support System")
                .Build();

            var builder = new ComponentBuilder()
                .WithButton("📩 فتح تيكت تويك (Tweak Ticket)", "create_tweak_ticket", ButtonStyle.Primary, new Emoji("🎫"));

            await channel.SendMessageAsync(embed: embed, components: builder.Build());
        }

        /// <summary>
        /// معالجة ضغطات الأزرار بخيط خلفي لتفادي حظر Gateway والرد الفوري
        /// </summary>
        private Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 1️⃣ عند ضغط زر فتح التيكت
                    if (component.Data.CustomId == "create_tweak_ticket")
                    {
                        // ⚡ استجابة فورية لتجنب خطأ انتهاء المهلة (3 ثوانٍ)
                        await component.DeferAsync(ephemeral: true);

                        var guild = (component.Channel as SocketGuildChannel)?.Guild;
                        var user = component.User as SocketGuildUser;

                        if (guild == null || user == null) return;

                        // 🧼 تنظيف اسم العضو ليقبل كمسمى قناة قانوني في ديسكورد
                        string cleanUsername = Regex.Replace(user.Username.ToLower(), @"[^a-z0-9]", "");
                        if (string.IsNullOrEmpty(cleanUsername)) cleanUsername = user.Id.ToString();

                        string channelName = $"tweak-{cleanUsername}";

                        // فحص وجود تيكت سابق مفتوح لنفس العميل
                        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
                        if (existingChannel != null)
                        {
                            await component.FollowupAsync($"❌ لديك تيكت مفتوح بالفعل هنا: {existingChannel.Mention}", ephemeral: true);
                            return;
                        }

                        // 📂 البحث عن فئة التيكتات أو إنشاؤها (باستخدام ICategoryChannel لتفادي خطأ CS0029)
                        string categoryName = "🎫 | تيكتات التويك";
                        ICategoryChannel category = guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

                        if (category == null)
                        {
                            // استخدام CreateCategoryChannelAsync لتفادي خطأ CS1061
                            category = await guild.CreateCategoryChannelAsync(categoryName);
                        }

                        // 📝 إنشاء روم التيكت وتخصيص الصلاحيات بالفئة
                        var ticketChannel = await guild.CreateTextChannelAsync(channelName, tcp =>
                        {
                            tcp.CategoryId = category.Id;
                            tcp.PermissionOverwrites = new[]
                            {
                                // إخفاء القناة عن باقي الأعضاء
                                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                                // إظهار القناة للعميل صاحب التيكت
                                new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    attachFiles: PermValue.Allow,
                                    readMessageHistory: PermValue.Allow)),
                                // إظهار القناة للبوت نفسه
                                new Overwrite(_client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    manageChannel: PermValue.Allow))
                            };
                        });

                        // الرسالة الترحيبية داخل التيكت مع الإشارة للروم المحدد
                        var welcomeEmbed = new EmbedBuilder()
                            .WithTitle($"🎫 أهلاً بك يا {user.Username} في تيكت التويك")
                            .WithDescription($"قبل أن نبدأ الشغل، يرجى التوجه إلى <#{InstructionsChannelId}> ومتابعة التعليمات والطلبات المذكورة هناك، وسيقوم الدعم بالرد عليك فوراً.\n\nلإغلاق التيكت بعد الانتهاء، اضغط على الزر أدناه.")
                            .WithColor(Color.Green)
                            .Build();

                        var closeButton = new ComponentBuilder()
                            .WithButton("🔒 إغلاق التيكت", "close_ticket", ButtonStyle.Danger, new Emoji("🛑"));

                        await ticketChannel.SendMessageAsync(text: $"{user.Mention}", embed: welcomeEmbed, components: closeButton.Build());
                        await component.FollowupAsync($"✅ تم إنشاء التيكت الخاص بك بنجاح في فئة التيكتات: {ticketChannel.Mention}", ephemeral: true);
                    }
                    // 2️⃣ عند ضغط زر إغلاق التيكت
                    else if (component.Data.CustomId == "close_ticket")
                    {
                        await component.DeferAsync(ephemeral: true);

                        if (component.Channel is SocketTextChannel channel)
                        {
                            await component.FollowupAsync("🔒 سيتم حذف وإغلاق هذا التيكت خلال 5 ثوانٍ...", ephemeral: true);
                            await Task.Delay(5000);

                            // التأكد من وجود القناة قبل الحذف لمنع الأخطاء الاستثنائية
                            var checkChannel = _client.GetChannel(channel.Id);
                            if (checkChannel != null)
                            {
                                await channel.DeleteAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TicketService Error]: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }
    }
}