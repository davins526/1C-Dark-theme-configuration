# Проверка обнаружения Defender

Обнаружение 0.2.2 остаётся открытым: Wacatac.B!ml у приложения,
Wacatac.C!ml у тестового процесса. Нельзя заранее утверждать, что оно ложное.

## Проверяемая сборка

Workflow **Verify Windows build and Defender scan** выполняется при изменении
исходников в main или вручную через вкладку Actions. Он предназначен для свежего
GitHub-hosted Windows Server 2022, а не для запуска на рабочем компьютере.

1. Проверяет логику отказа на обычных текстовых файлах с подставными ответами
   сканера. Это не антивирусная проверка приложения.
2. На временном GitHub-hosted worker запускает имеющуюся службу Defender,
   включает мониторинг в реальном времени и поведения, обновляет базы.
   Требует активной защиты и баз не старше двух суток. Отсутствующий/неработающий
   Defender завершает job ошибкой. На одноразовом worker удаляются предустановленные
   исключения путей, расширений и процессов, чтобы они не пропускали сканирование.
   Защита не отключается, исключения не добавляются, защита от изменений не меняется.
   Этот entry point отказывается работать локально
   и на self-hosted runner.
   Ошибка связи с запускающейся службой при обновлении баз допускает ещё две
   попытки с паузой 5 секунд; после третьей ошибки выполнение прекращается.
3. Проверяет подпись Microsoft у системного C#-компилятора. Скачивает официальный
   Zig 0.14.1 и сверяет закреплённый SHA-256 архива до его использования.
4. Собирает исходники и нейтральный фон. Личные файлы не загружаются.
   Полученные EXE/DLL не исполняются, подключения к 1С нет.
5. До сборки проверяет отдельный каталог в Windows Temp вне checkout на исключения.
   Копирует туда результаты со сверкой хешей, повторно проверяет каждый файл на
   исключения, записывает SHA-256,
   выполняет CustomScan для DLL, приложения и консольного помощника. Проверяет
   историю обнаружений для исходных путей и копий, наличие и неизменность файлов после
   сканирования, повторно проверяет защиту и исключения.
6. Сохраняет JSON-отчёты. Только после успешной проверки и повторной сверки хешей
   публикует экспериментальный кандидат: EXE, лицензии, отчёты, хеши и ссылку
   на коммит. Приложение этим workflow не исполняется. Карантин, ошибка
   сканера/журнала/проверки исключений или изменение файла запрещают публикацию кандидата.

Отчёты: `build.json` содержит коммит, этап, состояние защиты и хеш архива Zig;
также записывает число удалённых предустановленных исключений без публикации путей.
`defender.json`, если этап достигнут, — хеши файлов, версию движка/баз и итог.
Статусы `unverified` и `detected` означают неуспешную проверку. Даже
`no-detection-in-this-scan` относится только к указанным байтам и сканеру в этой
среде. Это не заключение Microsoft и не проверка поведения работающей программы.
Отказ из-за исключённого пути (`artifact-excluded`) отличается от ошибки команды
проверки исключений (`exclusion-tool-error`).
При неудаче до сканирования отчёта `defender.json` может не быть.

GitHub может предоставить образ без доступного Defender. Такой результат нужно
рассматривать как отсутствие проверки, а не как отсутствие угрозы. Нельзя
ослаблять проверки ради зелёной отметки. Повторяемость SHA сборки пока не доказана.

## Разбор Microsoft и подпись

Официальный путь оспаривания — [отправка файла как Software developer](https://www.microsoft.com/en-us/wdsi/filesubmission).
Для заявки нужны конкретный образец, его SHA-256, название обнаружения и описание
назначения. Не следует подменять заблокированный образец новым с другим хешем:
результат анализа относится к отправленным байтам. Если доступ к файлу блокируется,
не восстанавливайте его и не меняйте защиту ради этой инструкции.

Контекст для заявки (уточнить коммит/хеш и фактические результаты перед отправкой):

> Dark1C is an experimental open-source visual customization tool for the Windows
> 1C Configurator. Source: https://github.com/davins526/1C-Dark-theme-configuration.
> A local preview build was detected as Trojan:Win32/Wacatac.B!ml; a test executable
> was detected as Trojan:Win32/Wacatac.C!ml. We request analysis and a determination,
> not an allowlist without review. The application intentionally loads its own DLL
> into a user-selected, version-checked 1cv8 DESIGNER process using LoadLibraryW and
> CreateRemoteThread, then hooks drawing APIs with MinHook. OFF restores original
> drawing behavior; the DLL remains loaded until the target process exits.
> The reviewed source contains no network client, credential collection, database
> content access, persistence or antivirus configuration changes. This internal
> source review does not establish that the detected binary matches that source.

Заявка на разбор образца не отправлялась; ответа Microsoft пока нет.
Скрипты не выполняют отправку через портал; сам Defender работает со стандартными
настройками облачной защиты и телеметрии выданного worker.
Публиковать «Microsoft подтвердил ложное срабатывание» можно только после ответа
с привязкой к образцу и результату проверки.

Authenticode-подпись требует настоящего сертификата издателя и защиты закрытого
ключа; сертификата у проекта сейчас нет. Самоподписанный сертификат с установкой
корня пользователю не решает задачу. Подпись подтверждает издателя и целостность,
но не гарантирует отсутствие обнаружения Defender. Открытые вопросы загрузчика
и повышенных прав перечислены в [разборе исходников](SECURITY-REVIEW-2026-11-01.md).

Документация Microsoft: [CustomScan](https://learn.microsoft.com/en-us/powershell/module/defender/start-mpscan),
[включение мониторинга](https://learn.microsoft.com/en-us/defender-endpoint/configure-real-time-protection-microsoft-defender-antivirus),
[проверка исключений](https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-antivirus-exclusions-configure),
[оспаривание обнаружения](https://learn.microsoft.com/en-us/defender-xdr/submission-guide).
