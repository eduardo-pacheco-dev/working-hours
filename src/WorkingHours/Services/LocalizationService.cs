using System.Globalization;
using System.Windows;

namespace WorkingHours.Services;

/// <summary>
/// Tiny runtime localizer. Strings are pushed into the application resources as
/// <c>L.&lt;key&gt;</c>, so XAML can use <c>{DynamicResource L.Start}</c> and switch
/// language without reloading any view.
/// </summary>
public sealed class LocalizationService
{
    public const string Auto = "auto";

    private static readonly Dictionary<string, string> English = new()
    {
        ["AppName"] = "Working Hours",
        ["AppTagline"] = "A focused Pomodoro timer for Windows",
        ["Focus"] = "Focus",
        ["ShortBreak"] = "Short break",
        ["LongBreak"] = "Long break",
        ["RoundOf"] = "Round {0} of {1}",
        ["NextUp"] = "Next: {0}",
        ["Ready"] = "Ready",
        ["Paused"] = "Paused",
        ["InProgress"] = "In progress",
        ["Start"] = "Start",
        ["Pause"] = "Pause",
        ["Resume"] = "Resume",
        ["Reset"] = "Reset",
        ["Skip"] = "Skip",
        ["Restart"] = "Restart round",
        ["Settings"] = "Settings",
        ["Statistics"] = "Statistics",
        ["MiniMode"] = "Mini timer",
        ["Expand"] = "Expand",
        ["Tasks"] = "Tasks",
        ["AddTask"] = "Add task",
        ["NewTaskPlaceholder"] = "What needs to be done?",
        ["NoTasks"] = "No tasks yet. Add the first one.",
        ["ClearCompleted"] = "Clear completed",
        ["DeleteTask"] = "Delete task",
        ["StartTimer"] = "Start task stopwatch",
        ["PauseTimer"] = "Pause task stopwatch",
        ["DragToReorder"] = "Drag to reorder",
        ["AllDone"] = "All done",
        ["RemainingOne"] = "1 pending",
        ["RemainingMany"] = "{0} pending",
        ["PinOnTop"] = "Always on top",
        ["Unpin"] = "Stop keeping on top",
        ["Minimize"] = "Minimize",
        ["Close"] = "Close",
        ["Mute"] = "Mute",
        ["Unmute"] = "Unmute",
        ["Volume"] = "Volume",
        ["ShowWindow"] = "Show Working Hours",
        ["Exit"] = "Exit",
        ["ResumeTimer"] = "Resume timer",

        ["FocusCompleteTitle"] = "Focus round complete",
        ["FocusCompleteBody"] = "Nice work. Take a break — you earned it.",
        ["ShortBreakCompleteTitle"] = "Break over",
        ["ShortBreakCompleteBody"] = "Ready for the next focus round?",
        ["LongBreakCompleteTitle"] = "Long break over",
        ["LongBreakCompleteBody"] = "Fresh start. Let's get back to it.",

        ["Timer"] = "Timer",
        ["Cycle"] = "Cycle",
        ["Appearance"] = "Appearance",
        ["System"] = "System",
        ["Sound"] = "Sound",
        ["About"] = "About",
        ["FocusDuration"] = "Focus duration",
        ["ShortBreakDuration"] = "Short break",
        ["LongBreakDuration"] = "Long break",
        ["Minutes"] = "min",
        ["RoundsBeforeLongBreak"] = "Focus rounds before a long break",
        ["EnableShortBreaks"] = "Short breaks between focus rounds",
        ["EnableLongBreaks"] = "Long breaks after a full cycle",
        ["AutoStartWork"] = "Start focus rounds automatically",
        ["AutoStartBreaks"] = "Start breaks automatically",
        ["DialCountdown"] = "Dial counts down",
        ["DialHint"] = "When off, the ring fills up as time passes.",
        ["ThemeMode"] = "Theme mode",
        ["Auto"] = "Automatic",
        ["Light"] = "Light",
        ["Dark"] = "Dark",
        ["LightTheme"] = "Light theme",
        ["DarkTheme"] = "Dark theme",
        ["TrayIcon"] = "Show icon in the notification area",
        ["MinimizeToTray"] = "Minimize to the notification area",
        ["CloseToTray"] = "Keep running when the window is closed",
        ["AlwaysOnTop"] = "Keep the window always on top",
        ["Notifications"] = "Desktop notifications on round changes",
        ["GlobalShortcuts"] = "Global shortcuts",
        ["GlobalShortcutsHint"] = "Ctrl+F1 start/pause · Ctrl+F2 reset · Ctrl+F3 skip · Ctrl+F4 restart",
        ["VolumeLabel"] = "Alert volume",
        ["TickWork"] = "Soft tick during focus",
        ["TickBreak"] = "Soft tick during breaks",
        ["Save"] = "Save changes",
        ["Cancel"] = "Cancel",
        ["ResetStatistics"] = "Reset statistics",
        ["ResetStatisticsConfirm"] = "Delete all recorded focus sessions? This cannot be undone.",
        ["Language"] = "Language",
        ["SystemLanguage"] = "System language",
        ["English"] = "English",
        ["Portuguese"] = "Português (Brasil)",
        ["DataFolder"] = "Data folder",
        ["OpenDataFolder"] = "Open folder",
        ["Version"] = "Version {0}",
        ["AboutText"] = "A native Windows take on the Pomodoro Technique. All data stays on this computer.",
        ["License"] = "MIT License · inspired by Pomotroid",

        ["Today"] = "Today",
        ["ThisWeek"] = "This week",
        ["AllTime"] = "All time",
        ["TotalFocus"] = "Focus time",
        ["Sessions"] = "Sessions",
        ["CurrentStreak"] = "Current streak",
        ["LongestStreak"] = "Longest streak",
        ["BestDay"] = "Best day",
        ["NoDataYet"] = "No sessions recorded yet.",
        ["FocusByHour"] = "Focus by hour",
        ["FocusThisWeek"] = "Last seven days",
        ["FocusAllTime"] = "The last 52 weeks",
        ["Previous"] = "Previous",
        ["Next"] = "Next",
        ["Back"] = "Back",
        ["Days"] = "days",
        ["HoursShort"] = "h",
        ["MinutesShort"] = "m",
        ["NoData"] = "No data",
        ["StatisticsHint"] = "Sessions are recorded when a focus round finishes naturally.",
        ["ToastDismiss"] = "Dismiss"
    };

    private static readonly Dictionary<string, string> Portuguese = new()
    {
        ["AppName"] = "Working Hours",
        ["AppTagline"] = "Um timer Pomodoro focado para Windows",
        ["Focus"] = "Foco",
        ["ShortBreak"] = "Pausa curta",
        ["LongBreak"] = "Pausa longa",
        ["RoundOf"] = "Rodada {0} de {1}",
        ["NextUp"] = "A seguir: {0}",
        ["Ready"] = "Pronto",
        ["Paused"] = "Pausado",
        ["InProgress"] = "Em andamento",
        ["Start"] = "Iniciar",
        ["Pause"] = "Pausar",
        ["Resume"] = "Continuar",
        ["Reset"] = "Reiniciar",
        ["Skip"] = "Pular",
        ["Restart"] = "Reiniciar rodada",
        ["Settings"] = "Configurações",
        ["Statistics"] = "Estatísticas",
        ["MiniMode"] = "Mini relógio",
        ["Expand"] = "Expandir",
        ["Tasks"] = "Tarefas",
        ["AddTask"] = "Adicionar tarefa",
        ["NewTaskPlaceholder"] = "O que precisa ser feito?",
        ["NoTasks"] = "Nenhuma tarefa ainda. Adicione a primeira.",
        ["ClearCompleted"] = "Limpar concluídas",
        ["DeleteTask"] = "Excluir tarefa",
        ["StartTimer"] = "Iniciar cronômetro da tarefa",
        ["PauseTimer"] = "Pausar cronômetro da tarefa",
        ["DragToReorder"] = "Arraste para reordenar",
        ["AllDone"] = "Tudo em dia",
        ["RemainingOne"] = "1 pendente",
        ["RemainingMany"] = "{0} pendentes",
        ["PinOnTop"] = "Sempre no topo",
        ["Unpin"] = "Não manter no topo",
        ["Minimize"] = "Minimizar",
        ["Close"] = "Fechar",
        ["Mute"] = "Silenciar",
        ["Unmute"] = "Ativar som",
        ["Volume"] = "Volume",
        ["ShowWindow"] = "Mostrar Working Hours",
        ["Exit"] = "Sair",
        ["ResumeTimer"] = "Continuar timer",

        ["FocusCompleteTitle"] = "Rodada de foco concluída",
        ["FocusCompleteBody"] = "Bom trabalho. Faça uma pausa — você mereceu.",
        ["ShortBreakCompleteTitle"] = "Pausa encerrada",
        ["ShortBreakCompleteBody"] = "Pronto para a próxima rodada de foco?",
        ["LongBreakCompleteTitle"] = "Pausa longa encerrada",
        ["LongBreakCompleteBody"] = "Começo renovado. Vamos voltar ao foco.",

        ["Timer"] = "Tempo",
        ["Cycle"] = "Ciclo",
        ["Appearance"] = "Aparência",
        ["System"] = "Sistema",
        ["Sound"] = "Som",
        ["About"] = "Sobre",
        ["FocusDuration"] = "Duração do foco",
        ["ShortBreakDuration"] = "Pausa curta",
        ["LongBreakDuration"] = "Pausa longa",
        ["Minutes"] = "min",
        ["RoundsBeforeLongBreak"] = "Rodadas de foco antes da pausa longa",
        ["EnableShortBreaks"] = "Pausas curtas entre as rodadas de foco",
        ["EnableLongBreaks"] = "Pausa longa após um ciclo completo",
        ["AutoStartWork"] = "Iniciar rodadas de foco automaticamente",
        ["AutoStartBreaks"] = "Iniciar pausas automaticamente",
        ["DialCountdown"] = "Mostrador regressivo",
        ["DialHint"] = "Quando desligado, o anel se preenche com o passar do tempo.",
        ["ThemeMode"] = "Modo de tema",
        ["Auto"] = "Automático",
        ["Light"] = "Claro",
        ["Dark"] = "Escuro",
        ["LightTheme"] = "Tema claro",
        ["DarkTheme"] = "Tema escuro",
        ["TrayIcon"] = "Mostrar ícone na área de notificação",
        ["MinimizeToTray"] = "Minimizar para a área de notificação",
        ["CloseToTray"] = "Continuar em execução ao fechar a janela",
        ["AlwaysOnTop"] = "Manter a janela sempre no topo",
        ["Notifications"] = "Notificações da área de trabalho ao trocar de rodada",
        ["GlobalShortcuts"] = "Atalhos globais",
        ["GlobalShortcutsHint"] = "Ctrl+F1 iniciar/pausar · Ctrl+F2 reiniciar · Ctrl+F3 pular · Ctrl+F4 reiniciar rodada",
        ["VolumeLabel"] = "Volume dos alertas",
        ["TickWork"] = "Tique suave durante o foco",
        ["TickBreak"] = "Tique suave durante as pausas",
        ["Save"] = "Salvar alterações",
        ["Cancel"] = "Cancelar",
        ["ResetStatistics"] = "Apagar estatísticas",
        ["ResetStatisticsConfirm"] = "Excluir todas as sessões de foco registradas? Isso não pode ser desfeito.",
        ["Language"] = "Idioma",
        ["SystemLanguage"] = "Idioma do sistema",
        ["English"] = "English",
        ["Portuguese"] = "Português (Brasil)",
        ["DataFolder"] = "Pasta de dados",
        ["OpenDataFolder"] = "Abrir pasta",
        ["Version"] = "Versão {0}",
        ["AboutText"] = "Uma versão nativa para Windows da Técnica Pomodoro. Todos os dados ficam neste computador.",
        ["License"] = "Licença MIT · inspirado no Pomotroid",

        ["Today"] = "Hoje",
        ["ThisWeek"] = "Esta semana",
        ["AllTime"] = "Todo o período",
        ["TotalFocus"] = "Tempo de foco",
        ["Sessions"] = "Sessões",
        ["CurrentStreak"] = "Sequência atual",
        ["LongestStreak"] = "Maior sequência",
        ["BestDay"] = "Melhor dia",
        ["NoDataYet"] = "Nenhuma sessão registrada ainda.",
        ["FocusByHour"] = "Foco por hora",
        ["FocusThisWeek"] = "Últimos sete dias",
        ["FocusAllTime"] = "As últimas 52 semanas",
        ["Previous"] = "Anterior",
        ["Next"] = "Próximo",
        ["Back"] = "Voltar",
        ["Days"] = "dias",
        ["HoursShort"] = "h",
        ["MinutesShort"] = "min",
        ["NoData"] = "Sem dados",
        ["StatisticsHint"] = "As sessões são registradas quando uma rodada de foco termina naturalmente.",
        ["ToastDismiss"] = "Dispensar"
    };

    private Dictionary<string, string> _catalog = English;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage { get; private set; } = "en";
    public CultureInfo Culture => _culture;

    public string this[string key] => _catalog.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object[] args) => string.Format(_culture, this[key], args);

    public void Apply(string languageSetting)
    {
        var language = ResolveLanguage(languageSetting);
        _catalog = language == "pt-BR" ? Portuguese : English;
        CurrentLanguage = language;
        _culture = CultureInfo.GetCultureInfo(language);

        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;

        foreach (var pair in English)
            Application.Current.Resources["L." + pair.Key] = pair.Value;
        if (!ReferenceEquals(_catalog, English))
        {
            foreach (var pair in _catalog)
                Application.Current.Resources["L." + pair.Key] = pair.Value;
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string ResolveLanguage(string setting)
    {
        if (string.Equals(setting, "pt-BR", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";
        if (string.Equals(setting, "en", StringComparison.OrdinalIgnoreCase))
            return "en";
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en";
    }
}
