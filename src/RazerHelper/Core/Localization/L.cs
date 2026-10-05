using System.Globalization;

namespace RazerHelper.Core.Localization;

/// <summary>The languages the interface is written in.</summary>
internal enum AppLanguage
{
    English,
    Spanish
}

/// <summary>
/// Translates the interface. Every on-screen text is written in English in the
/// code and passed through <see cref="T"/> (or <see cref="F"/> when it carries
/// values); in Spanish the English text is looked up in <see cref="Spanish"/>.
/// A text without a translation shows in English rather than not at all.
/// The language is chosen once at startup: changing it restarts the app.
/// </summary>
internal static class L
{
    /// <summary>The language on screen. English until startup picks one, which keeps the tests in English.</summary>
    public static AppLanguage Current { get; set; } = AppLanguage.English;

    /// <summary>The setting's value for each language, as stored in settings.json.</summary>
    public static string Code(AppLanguage language) => language == AppLanguage.Spanish ? "es" : "en";

    /// <summary>
    /// The language for a stored setting: "es" or "en", or, with none stored,
    /// the Windows display language (Spanish for any Spanish, English otherwise).
    /// </summary>
    public static AppLanguage Resolve(string? code, CultureInfo windowsCulture) => code?.ToLowerInvariant() switch
    {
        "es" => AppLanguage.Spanish,
        "en" => AppLanguage.English,
        _ => windowsCulture.TwoLetterISOLanguageName == "es" ? AppLanguage.Spanish : AppLanguage.English
    };

    /// <summary>The text in the current language.</summary>
    public static string T(string english) =>
        Current == AppLanguage.Spanish && Spanish.TryGetValue(english, out var spanish) ? spanish : english;

    /// <summary>A text with values: <paramref name="englishFormat"/> uses {0}, {1}... as string.Format does.</summary>
    public static string F(string englishFormat, params object?[] values) =>
        string.Format(CultureInfo.InvariantCulture, T(englishFormat), values);

    internal static readonly IReadOnlyDictionary<string, string> Spanish = new Dictionary<string, string>
    {
        // Tray icon and window titles.
        ["Free up GPU"] = "Liberar GPU",
        ["Settings"] = "Ajustes",
        ["Open RazerHelper"] = "Abrir RazerHelper",
        ["Exit"] = "Salir",
        ["Close"] = "Cerrar",
        ["Close (RazerHelper keeps running in the tray)"] = "Cerrar (RazerHelper sigue en la bandeja)",

        // The sidebar's sections and their pages.
        ["Performance"] = "Rendimiento",
        ["Display and lighting"] = "Pantalla e iluminación",
        ["Energy"] = "Energía",
        ["System"] = "Sistema",
        ["More details"] = "Más detalles",
        ["Power source profiles"] = "Perfiles por fuente de energía",
        ["Power plan"] = "Plan de energía",
        ["The one Windows uses."] = "El que usa Windows.",
        ["Install Razer Blade plan"] = "Instalar plan Razer Blade",
        ["Installing..."] = "Instalando...",
        ["Razer Blade plan installed and in use."] = "Plan Razer Blade instalado y en uso.",
        ["Could not install the Razer Blade plan."] = "No se pudo instalar el plan Razer Blade.",
        ["Windows did not change the plan."] = "Windows no cambió el plan.",
        ["When you plug in or unplug the charger, switches to the profile chosen for each."] = "Al conectar o desconectar el cargador, cambia al perfil elegido para cada caso.",

        // Optimize.
        ["Optimize"] = "Optimizar",
        ["Free up"] = "Liberar",
        ["Clean"] = "Limpiar",
        ["Free up memory"] = "Liberar memoria",
        ["Frees unused RAM and clears Windows' cache (asks for permission)."] = "Libera la RAM sin usar y vacía la caché de Windows (pide permiso).",
        ["Temporary files"] = "Archivos temporales",
        ["Scanning..."] = "Analizando...",
        ["Closes the apps keeping the dedicated GPU awake, if you confirm."] = "Cierra las apps que mantienen activa la GPU dedicada, si lo confirmas.",
        ["Freeing up memory..."] = "Liberando memoria...",
        ["{0} freed from {1} programs in the background."] = "{0} liberados de {1} programas en segundo plano.",
        ["Could not free up memory."] = "No se pudo liberar la memoria.",
        ["The memory cache was kept: administrator permission was not given."] = "La caché de memoria se mantuvo: no se dio permiso de administrador.",
        ["The memory cache could not be cleared."] = "No se pudo vaciar la caché de memoria.",
        ["{0} of memory cache cleared."] = "{0} de caché de memoria vaciados.",
        ["Nothing to clean: no temporary files over a day old."] = "Nada que limpiar: no hay archivos temporales de más de un día.",
        ["{0} in {1} files over a day old."] = "{0} en {1} archivos de más de un día.",
        ["Could not read the temporary folder."] = "No se pudo leer la carpeta temporal.",
        ["Cleaning..."] = "Limpiando...",
        ["{0} freed ({1} files)."] = "{0} liberados ({1} archivos).",
        ["{0} in use were left."] = "{0} en uso se quedaron.",
        ["Could not clean the temporary files."] = "No se pudieron limpiar los archivos temporales.",
        ["Nothing to clean right now."] = "Nada que limpiar por ahora.",
        ["{0} to clean: temporary files, Recycle Bin and Windows Update."] = "{0} para limpiar: archivos temporales, papelera y Windows Update.",
        ["Empty the Recycle Bin too? It holds {0} in {1} items, which cannot be recovered afterwards."] = "¿Vaciar también la papelera? Tiene {0} en {1} elementos, que no se podrán recuperar después.",
        ["{0} freed."] = "{0} liberados.",
        ["Windows' caches were kept: administrator permission was not given."] = "Las cachés de Windows se mantuvieron: no se dio permiso de administrador.",
        ["Windows' caches could not all be cleared."] = "No se pudieron vaciar todas las cachés de Windows.",
        ["NVIDIA shader cache"] = "Caché de sombreadores de NVIDIA",
        ["Clear"] = "Vaciar",
        ["Clearing..."] = "Vaciando...",
        ["Empty. Games rebuild it as they load."] = "Vacía. Los juegos la rehacen al cargar.",
        ["{0} in {1} files. Clearing it can fix stutter after a driver update."] = "{0} en {1} archivos. Vaciarla puede quitar tirones tras actualizar el driver.",
        ["Could not read the shader cache."] = "No se pudo leer la caché de sombreadores.",
        ["Could not clear the shader cache."] = "No se pudo vaciar la caché de sombreadores.",
        ["No NVIDIA shader cache on this PC."] = "No hay caché de sombreadores de NVIDIA en este equipo.",
        ["Hibernation"] = "Hibernación",
        ["Off, with no file on disk. Hibernate and fast startup are off too."] = "Desactivada, sin archivo en el disco. La hibernación y el inicio rápido también.",
        ["Its file takes {0}. Off frees it, with no hibernate or fast startup."] = "Su archivo ocupa {0}. Desactivarla lo libera, sin hibernar ni inicio rápido.",
        ["Off frees its file's disk space, but turns off hibernate and fast startup."] = "Desactivarla libera el espacio de su archivo, pero quita la hibernación y el inicio rápido.",
        ["Asking Windows (asks for permission)..."] = "Pidiéndolo a Windows (pide permiso)...",
        ["Kept as it was: administrator permission was not given."] = "Se dejó como estaba: no se dio permiso de administrador.",
        ["Windows did not make the change."] = "Windows no hizo el cambio.",

        // Status messages.
        ["{0} shortcut is used by another program."] = "Otro programa ya usa el atajo {0}.",
        ["{0} control interface found."] = "Control de {0} encontrado.",
        ["{0} control interface found (community-reported product id, not verified on this model)."] =
            "Control de {0} encontrado (identificador aportado por la comunidad, sin verificar en este modelo).",
        ["No supported Razer laptop detected; fan and battery controls unavailable."] =
            "No se detectó un portátil Razer compatible; los controles de ventilador y batería no están disponibles.",

        // Settings window.
        ["Language"] = "Idioma",
        ["RazerHelper restarts to change the language."] = "RazerHelper se reinicia para cambiar el idioma.",
        ["Start at login"] = "Iniciar con Windows",
        ["Opens in the tray when you sign in."] = "Se abre en la bandeja al iniciar sesión.",
        ["Hide when clicking away"] = "Ocultar al hacer clic fuera",
        ["Off, it stays open until you click the tray icon."] = "Si no, sigue abierta hasta pulsar el icono de la bandeja.",
        ["Always on top"] = "Siempre visible",
        ["Stays above other windows and games. {0} shows or hides it."] = "Queda sobre otras ventanas y juegos. {0} la muestra u oculta.",
        ["Free up GPU when unplugged"] = "Liberar GPU sin cargador",
        ["Offers to close apps using the dedicated GPU, to save battery."] = "Ofrece cerrar las apps que usan la GPU dedicada, para ahorrar batería.",
        ["Performance mode shortcuts"] = "Atajos de los modos de rendimiento",
        ["Ctrl+Shift+F1 and F2 switch to Balanced and Silent."] = "Ctrl+Shift+F1 y F2 cambian a Equilibrado y Silencio.",
        ["Keyboard off with the screen"] = "Teclado apagado con la pantalla",
        ["Turns the lighting off and back on with the screen."] = "Apaga y vuelve a encender la luz junto con la pantalla.",
        ["Maintenance"] = "Mantenimiento",
        ["Razer drivers and support, logs, reset."] = "Drivers y soporte de Razer, registros, restablecer.",
        ["Drivers"] = "Drivers",
        ["Logs"] = "Registros",
        ["Reset"] = "Restablecer",
        ["Reset to defaults"] = "Restablecer valores",
        ["Could not change Start at login."] = "No se pudo cambiar Iniciar con Windows.",
        ["Reset RazerHelper to how it was the first time you opened it?\r\n\r\nThis will:\r\n  - clear your saved settings, including the options in this window and your never-close list\r\n  - turn off Start at login\r\n  - set the laptop to Balanced mode with no battery charge limit\r\n\r\nIt will not change Razer's background services or anything else on your PC. RazerHelper will restart."] =
            "¿Dejar RazerHelper como estaba la primera vez que lo abriste?\r\n\r\nEsto:\r\n  - borra tus ajustes guardados, incluidas las opciones de esta ventana y tu lista de apps que nunca se cierran\r\n  - desactiva Iniciar con Windows\r\n  - pone el portátil en modo Equilibrado sin límite de carga\r\n\r\nNo cambia los servicios de Razer ni nada más de tu PC. RazerHelper se reiniciará.",
        ["Most of the reset worked, but not everything:"] = "Casi todo se restableció, pero no todo:",
        ["RazerHelper will restart now. Details are in the log."] = "RazerHelper se reiniciará ahora. Los detalles están en el registro.",
        ["The reset is done, but RazerHelper could not restart itself. Please close it from the tray icon and open it again."] =
            "Se restableció, pero RazerHelper no pudo reiniciarse solo. Ciérralo desde el icono de la bandeja y vuelve a abrirlo.",
        ["RazerHelper could not restart itself. Please close it from the tray icon and open it again."] =
            "RazerHelper no pudo reiniciarse solo. Ciérralo desde el icono de la bandeja y vuelve a abrirlo.",
        ["Your saved settings could not be cleared."] = "No se pudieron borrar tus ajustes guardados.",
        ["Start at login could not be turned off."] = "No se pudo desactivar Iniciar con Windows.",
        ["The laptop could not be set to Balanced mode."] = "No se pudo poner el portátil en modo Equilibrado.",
        ["The battery charge limit could not be removed."] = "No se pudo quitar el límite de carga.",

        // Free up GPU.
        ["Save battery?"] = "¿Ahorrar batería?",
        ["Ask them to close"] = "Pedir que se cierren",
        ["Not now"] = "Ahora no",
        ["These apps are keeping the dedicated GPU awake, which drains the battery:"] =
            "Estas apps mantienen despierta la GPU dedicada y gastan batería:",
        ["Ask them to close? Each one can still ask you to save your work first."] =
            "¿Pedirles que se cierren? Cada una puede pedirte antes que guardes tu trabajo.",
        ["{0} instances, "] = "{0} instancias, ",
        ["No dedicated GPU was found, so there is nothing to free up."] = "No se encontró GPU dedicada, así que no hay nada que liberar.",
        ["An external display is connected (or Windows could not say). It is driven by the dedicated GPU, so the GPU stays on whatever is closed. Disconnect it and try again."] =
            "Hay una pantalla externa conectada (o Windows no pudo confirmarlo). La mueve la GPU dedicada, así que la GPU sigue encendida aunque se cierre todo. Desconéctala y vuelve a intentarlo.",
        ["No apps that can be closed are using the dedicated GPU. Windows, drivers, terminals, editors and background helpers are left alone."] =
            "Ninguna app que se pueda cerrar está usando la GPU dedicada. Windows, drivers, terminales, editores y procesos de fondo no se tocan.",
        ["Things changed while you were deciding, so nothing was closed. Try again."] =
            "Algo cambió mientras decidías, así que no se cerró nada. Vuelve a intentarlo.",
        ["Already checking the dedicated GPU."] = "Ya se está revisando la GPU dedicada.",
        ["Could not check the dedicated GPU. Details are in the log."] = "No se pudo revisar la GPU dedicada. Los detalles están en el registro.",

        // Performance.
        ["Performance Mode"] = "Modo de rendimiento",
        ["Balanced"] = "Equilibrado",
        ["Gaming"] = "Juego",
        ["Custom"] = "Personalizado",
        ["Speed"] = "Velocidad",
        ["Temperature"] = "Temperatura",
        ["Frequency"] = "Frecuencia",
        ["Available"] = "Disponible",
        ["Asleep"] = "En reposo",
        ["Silent"] = "Silencio",
        ["Low"] = "Bajo",
        ["Medium"] = "Medio",
        ["High"] = "Alto",
        ["Boost"] = "Turbo",
        ["Needs to be plugged in"] = "Requiere el cargador conectado",
        ["Not supported on this laptop"] = "No compatible con este portátil",
        ["This laptop does not support that mode."] = "Este portátil no admite ese modo.",
        ["Active"] = "Activo",
        ["Busy, try again in a moment"] = "Ocupado, inténtalo de nuevo en un momento",
        ["Could not change max fan speed."] = "No se pudo cambiar la velocidad máxima del ventilador.",
        ["Could not apply the power profile."] = "No se pudo aplicar el perfil de energía.",
        ["Could not change the performance mode."] = "No se pudo cambiar el modo de rendimiento.",
        ["Could not change the boost level."] = "No se pudo cambiar el nivel de turbo.",
        ["Performance profile applied."] = "Perfil de rendimiento aplicado.",

        // Fans.
        ["Fans"] = "Ventiladores",
        ["CPU Fan"] = "Ventilador CPU",
        ["GPU Fan"] = "Ventilador GPU",
        ["Max"] = "Máx.",
        ["Automatic RPM"] = "RPM Automática",
        ["The system sets the speed as needed"] = "El sistema elige la velocidad según el uso",
        ["Max RPM"] = "RPM Máxima",
        ["Always runs at 100%, however it is used"] = "Siempre al 100 %, sin importar el uso",
        ["Needs Custom mode, plugged in"] = "Requiere modo Personalizado y el cargador conectado",
        ["Needs to be plugged in, and not in Silent mode"] = "Requiere el cargador conectado y no estar en modo Silencio",
        ["This laptop does not support max fan speed"] = "Este portátil no permite la velocidad máxima del ventilador",

        // Display.
        ["Display"] = "Pantalla",
        ["Color profile"] = "Perfil de color",
        ["Standard"] = "Estándar",
        ["Warm"] = "Cálido",
        ["Cool"] = "Frío",
        ["Contrast"] = "Contraste",
        ["This screen does not take it"] = "Esta pantalla no lo admite",
        ["Current: -- Hz"] = "Actual: -- Hz",
        ["Auto: power source unavailable."] = "Auto: no se sabe si está enchufado.",
        ["Auto: waiting for the game"] = "Auto: esperando al juego",
        ["Not available"] = "No disponible",
        ["{0} Hz is not available for the current display mode."] = "{0} Hz no está disponible en el modo de pantalla actual.",
        ["Windows could not apply {0} Hz. Error: {1}."] = "Windows no pudo aplicar {0} Hz. Error: {1}.",
        ["Switched to {0} Hz."] = "Cambiado a {0} Hz.",
        ["{0} Hz is not available."] = "{0} Hz no está disponible.",
        ["{0} Hz is not available at {1}x{2}."] = "{0} Hz no está disponible a {1}x{2}.",

        // Lighting.
        ["Lighting"] = "Iluminación",
        ["Keyboard"] = "Teclado",
        ["Logo"] = "Logo",
        ["Off"] = "Apagado",
        ["On"] = "Encendido",
        ["Static"] = "Fijo",
        ["Static green"] = "Verde fijo",
        ["Spectrum"] = "Espectro",
        ["Breathing"] = "Respiración",
        ["Wave"] = "Onda",
        ["White"] = "Blanco",
        ["Razer green"] = "Verde Razer",
        ["Red"] = "Rojo",
        ["Orange"] = "Naranja",
        ["Yellow"] = "Amarillo",
        ["Cyan"] = "Cian",
        ["Blue"] = "Azul",
        ["Purple"] = "Morado",
        ["Pink"] = "Rosa",
        ["Could not change the keyboard lighting."] = "No se pudo cambiar la luz del teclado.",
        ["Could not change the keyboard brightness."] = "No se pudo cambiar el brillo del teclado.",
        ["Could not change the logo lighting."] = "No se pudo cambiar la luz del logo.",
        ["Could not change the logo brightness."] = "No se pudo cambiar el brillo del logo.",
        ["Could not change the keyboard color."] = "No se pudo cambiar el color del teclado.",
        ["Could not read the lighting state."] = "No se pudo leer el estado de la iluminación.",
        ["Could not read the lighting state after a change."] = "No se pudo leer la iluminación después del cambio.",

        // Battery.
        ["Battery Charge Limit"] = "Límite de carga",
        ["Limit:"] = "Límite:",
        ["Plugged in"] = "Enchufado",
        ["On battery"] = "Con batería",
        ["This laptop does not support a battery charge limit"] = "Este portátil no permite limitar la carga",
        ["Could not restore the saved battery charge limit ({0}%)."] = "No se pudo recuperar el límite de carga guardado ({0} %).",
        ["Battery charge limit disabled. Charging is allowed to 100%."] = "Límite de carga desactivado. Carga hasta el 100 %.",
        ["Battery charge limit set to {0}%."] = "Límite de carga fijado en {0} %.",
        ["Battery charge-limit change to {0}% failed."] = "Falló el cambio del límite de carga a {0} %.",
        ["Could not change the battery charge limit."] = "No se pudo cambiar el límite de carga.",

        // Battery window.
        ["Battery"] = "Batería",
        ["Unavailable"] = "No disponible",
        ["No information"] = "Sin información",
        ["Windows did not report the battery"] = "Windows no informó de la batería",
        ["Windows did not report these figures"] = "Windows no informó de estos datos",
        ["Power"] = "Potencia",
        ["Time"] = "Tiempo",
        ["Charge"] = "Carga",
        ["Health"] = "Salud",
        ["Voltage"] = "Voltaje",
        ["Cycles"] = "Ciclos",

        // System information window.
        ["System information"] = "Información del equipo",
        ["Reading..."] = "Leyendo...",
        ["Model"] = "Modelo",
        ["Operating system"] = "Sistema operativo",
        ["Integrated GPU"] = "GPU integrada",
        ["{0} of {1}"] = "{0} de {1}",
        ["{0} free"] = "{0} libres",
        ["build {0}"] = "compilación {0}",
        ["{0} cores · {1} threads"] = "{0} núcleos · {1} hilos",
        ["{0} threads"] = "{0} hilos",

        ["Charging"] = "Cargando",
        ["Using"] = "Consumiendo",
        ["left"] = "restante",
        ["to full"] = "para cargar",
        ["of {0}"] = "de {0}",
        ["{0} of {1} (factory)"] = "{0} de {1} (fábrica)",
        ["Plugged in, not charging"] = "Enchufado, sin cargar",
        ["Lithium-ion"] = "Ion de litio",
        ["Lithium polymer"] = "Polímero de litio",
        ["Nickel-metal hydride"] = "Níquel-metalhidruro",
        ["Nickel-cadmium"] = "Níquel-cadmio",
        ["Lead-acid"] = "Plomo-ácido",

        // Razer software.
        ["Razer Software Running: --"] = "Software Razer activo: --",
        ["Razer Software Running: {0}"] = "Software Razer activo: {0}",
        ["Razer Software Running: 0 (starts at login)"] = "Software Razer activo: 0 (arranca con Windows)",
        ["Razer Software Running: 0"] = "Software Razer activo: 0",
        ["Stop"] = "Detener",
        ["Start"] = "Iniciar",
        ["Tip: uninstall Razer Synapse for the cleanest experience."] = "Consejo: desinstala Razer Synapse para una experiencia más limpia.",
        ["Could not change the Razer software."] = "No se pudo cambiar el software de Razer.",
        ["Stop Razer software"] = "Detener software de Razer",
        ["Stopping Razer software..."] = "Deteniendo software de Razer...",
        ["Starting Razer software..."] = "Iniciando software de Razer...",
        ["Administrator approval was declined. Nothing was changed."] = "Se rechazó el permiso de administrador. No se cambió nada.",
        ["Some Razer services could not be stopped."] = "Algunos servicios de Razer no se pudieron detener.",
        ["Razer's startup at login could not be turned off."] = "No se pudo desactivar el arranque de Razer con Windows.",
        ["{0} See the log for details."] = "{0} Mira el registro para más detalles.",
        ["Razer software stopped and kept off."] = "Software de Razer detenido y desactivado.",
        ["Some Razer services could not be restored."] = "Algunos servicios de Razer no se pudieron restaurar.",
        ["Razer's startup at login could not be restored."] = "No se pudo restaurar el arranque de Razer con Windows.",
        ["Razer software restored."] = "Software de Razer restaurado.",
        ["Stop Razer's background software?"] = "¿Detener el software de fondo de Razer?",
        ["These {0} services will be stopped and kept off, including after a restart:"] =
            "Estos {0} servicios se detendrán y seguirán apagados, también tras reiniciar:",
        ["These Razer programs are running and will be asked to close (nothing is force-closed):"] =
            "Estos programas de Razer están abiertos y se les pedirá que se cierren (nada se cierra a la fuerza):",
        ["Razer will be stopped from starting when you sign in, the same as switching it off in Task Manager's Startup tab:"] =
            "Razer dejará de arrancar al iniciar sesión, igual que al desactivarlo en la pestaña Inicio del Administrador de tareas:",
        ["Razer devices connected now: {0}."] = "Dispositivos Razer conectados ahora: {0}.",
        ["No other Razer devices are connected right now."] = "No hay otros dispositivos Razer conectados ahora.",
        ["While the services are off, Razer-only features can't be configured on those devices (button remapping, macros, lighting effects, DPI stages). The devices still work as normal."] =
            "Con los servicios apagados, las funciones exclusivas de Razer no se pueden configurar en esos dispositivos (reasignar botones, macros, efectos de luz, niveles de DPI). Los dispositivos siguen funcionando con normalidad.",
        ["Press Start to bring everything back exactly as it was."] = "Pulsa Iniciar para dejarlo todo exactamente como estaba.",
        ["Windows will ask for administrator approval."] = "Windows pedirá permiso de administrador.",
        ["{0} running = {1} services + {2} Razer programs"] = "{0} activos = {1} servicios + {2} programas de Razer",
        ["Services: {0} of {1} running"] = "Servicios: {0} de {1} activos",
        ["  ({0} not yet disabled)"] = "  ({0} aún sin desactivar)",
        ["Programs: none running"] = "Programas: ninguno abierto",
        ["Programs running:"] = "Programas abiertos:",
        ["Checked at {0}"] = "Revisado a las {0}",
        ["Start at login: no Razer entry found"] = "Arranque con Windows: no hay entrada de Razer",
        ["Start at login: ON ({0})"] = "Arranque con Windows: SÍ ({0})",
        ["Start at login: off"] = "Arranque con Windows: no",

        // Errors.
        ["RazerHelper hit an unexpected error but is still running.\n\nIf something stops working, restart it. Details were saved to:\n"] =
            "RazerHelper tuvo un error inesperado pero sigue funcionando.\n\nSi algo deja de funcionar, reinícialo. Los detalles se guardaron en:\n",
        ["RazerHelper hit an unexpected error and has to close.\n\nDetails were saved to:\n"] =
            "RazerHelper tuvo un error inesperado y tiene que cerrarse.\n\nLos detalles se guardaron en:\n",
    };
}
