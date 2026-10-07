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

        // 📌 ضع آيدي (ID) قناة التقييمات (client-reviews) هنا
        private const ulong ClientReviewsChannelId = 1557174092744892476; 

        public TicketService(DiscordSocketClient client)
        {
            _client = client;
            _client.ButtonExecuted += OnButtonExecutedAsync;
            _client.ModalSubmitted += OnModalSubmittedAsync; // تسجيل حدث استقبال النوافذ المنبثقة
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
        /// معالجة ضغطات الأزرار
        /// </summary>
        private Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 1️⃣ فتح التيكت
                    if (component.Data.CustomId == "create_tweak_ticket")
                    {
                        await component.DeferAsync(ephemeral: true);

                        var guild = (component.Channel as SocketGuildChannel)?.Guild;
                        var user = component.User as SocketGuildUser;

                        if (guild == null || user == null) return;

                        string cleanUsername = Regex.Replace(user.Username.ToLower(), @"[^a-z0-9]", "");
                        if (string.IsNullOrEmpty(cleanUsername)) cleanUsername = user.Id.ToString();

                        string channelName = $"tweak-{cleanUsername}";

                        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
                        if (existingChannel != null)
                        {
                            await component.FollowupAsync($"❌ لديك تيكت مفتوح بالفعل هنا: {existingChannel.Mention}", ephemeral: true);
                            return;
                        }

                        string categoryName = "🎫 | تيكتات التويك";
                        ICategoryChannel category = guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

                        if (category == null)
                        {
                            category = await guild.CreateCategoryChannelAsync(categoryName);
                        }

                        var ticketChannel = await guild.CreateTextChannelAsync(channelName, tcp =>
                        {
                            tcp.CategoryId = category.Id;
                            tcp.PermissionOverwrites = new[]
                            {
                                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                                new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    attachFiles: PermValue.Allow,
                                    readMessageHistory: PermValue.Allow)),
                                new Overwrite(_client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    manageChannel: PermValue.Allow))
                            };
                        });

                        var welcomeEmbed = new EmbedBuilder()
                            .WithTitle($"🎫 أهلاً بك يا {user.Username} في تيكت التويك")
                            .WithDescription($"قبل أن نبدأ الشغل، يرجى التوجه إلى <#{InstructionsChannelId}> ومتابعة التعليمات والطلبات المذكورة هناك، وسيقوم الدعم بالرد عليك فوراً.\n\nيمكنك تقييم الخدمة أو إغلاق التيكت عبر الأزرار أدناه.")
                            .WithColor(Color.Green)
                            .Build();

                        // 🔘 أزرار التيكت: زر التقييم + زر الإغلاق
                        var actionButtons = new ComponentBuilder()
                            .WithButton("⭐ تقييم الخدمة", "rate_ticket", ButtonStyle.Success, new Emoji("⭐"))
                            .WithButton("🔒 إغلاق التيكت", "close_ticket", ButtonStyle.Danger, new Emoji("🛑"));

                        await ticketChannel.SendMessageAsync(text: $"{user.Mention}", embed: welcomeEmbed, components: actionButtons.Build());
                        await component.FollowupAsync($"✅ تم إنشاء التيكت الخاص بك بنجاح: {ticketChannel.Mention}", ephemeral: true);
                    }
                    // 2️⃣ ضغط زر التقييم (إظهار نافذة التقييم المنبثقة)
                    else if (component.Data.CustomId == "rate_ticket")
                    {
                        var modal = new ModalBuilder()
                            .WithTitle("⭐ تقييم جلسة التويك - BaderTweaker")
                            .WithCustomId("tweak_review_modal")
                            .AddTextInput("عدد النجوم (اختر من 1 إلى 5)", "star_rating", TextInputStyle.Short, placeholder: "اكتب رقماً من 1 إلى 5", required: true, maxLength: 1)
                            .AddTextInput("رأيك وانطباعك عن التويك", "review_feedback", TextInputStyle.Paragraph, placeholder: "اكتب رأيك وتجربتك بعد الجلسة بالتفصيل...", required: true);

                        // إظهار النافذة للمستخدم (ملاحظة: لا يُستخدم DeferAsync قبل إظهار Modal)
                        await component.RespondWithModalAsync(modal.Build());
                    }
                    // 3️⃣ إغلاق التيكت
                    else if (component.Data.CustomId == "close_ticket")
                    {
                        await component.DeferAsync(ephemeral: true);

                        if (component.Channel is SocketTextChannel channel)
                        {
                            await component.FollowupAsync("🔒 سيتم حذف وإغلاق هذا التيكت خلال 5 ثوانٍ...", ephemeral: true);
                            await Task.Delay(5000);

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
                    Console.WriteLine($"[TicketService Button Error]: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// معالجة استلام تقييم العميل ونشره في شات client-reviews
        /// </summary>
        private Task OnModalSubmittedAsync(SocketModal modal)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (modal.Data.CustomId == "tweak_review_modal")
                    {
                        await modal.DeferAsync(ephemeral: true);

                        var guild = (modal.Channel as SocketGuildChannel)?.Guild;
                        var user = modal.User;

                        if (guild == null) return;

                        // استخراج المدخلات من النموذج
                        var components = modal.Data.Components.ToList();
                        string rawRating = components.FirstOrDefault(x => x.CustomId == "star_rating")?.Value ?? "5";
                        string feedback = components.FirstOrDefault(x => x.CustomId == "review_feedback")?.Value ?? "لا يوجد تعليق.";

                        // تحويل الرقم إلى نجوم
                        int.TryParse(rawRating, out int starsCount);
                        if (starsCount < 1) starsCount = 1;
                        if (starsCount > 5) starsCount = 5;

                        string starsDisplay = new string('⭐', starsCount);

                        // بناء كرت التقييم الفخم
                        var reviewEmbed = new EmbedBuilder()
                            .WithTitle("🌟 تقييم جديد لخدمة BaderTweaker")
                            .WithColor(new Color(255, 215, 0)) // لون ذهبي
                            .AddField("👤 العميل", user.Mention, inline: true)
                            .AddField("⭐ التقييم", $"{starsDisplay} ({starsCount}/5)", inline: true)
                            .AddField("💬 رأي وانطباع العميل", feedback)
                            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                            .WithFooter("BaderTweaker Customer Reviews")
                            .WithCurrentTimestamp()
                            .Build();

                        // البحث عن شات التقييمات عبر الـ ID أو بالاسم تلقائياً
                        var reviewChannel = guild.GetTextChannel(ClientReviewsChannelId) 
                                           ?? guild.TextChannels.FirstOrDefault(c => c.Name.Contains("client-reviews") || c.Name.Contains("التقييمات"));

                        if (reviewChannel != null)
                        {
                            await reviewChannel.SendMessageAsync(embed: reviewEmbed);
                            await modal.FollowupAsync("✅ شكرًا لك! تم إرسال تقييمك ونشره بنجاح في شات التقييمات.", ephemeral: true);
                        }
                        else
                        {
                            await modal.FollowupAsync("✅ تم تسجيل تقييمك بنجاح! (تنبيه: لم يتم العثور على شات التقييمات لنشره تلقائياً).", ephemeral: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Modal Error]: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }
    }
}
