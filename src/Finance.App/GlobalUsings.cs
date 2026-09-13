// Короткое имя Application внутри Finance.App разрешается в пространство имён
// Finance.Application: вложенное пространство имён побеждает и обычный using,
// и псевдоним с тем же именем. Поэтому тип MAUI зовётся здесь иначе.
global using ControlsApplication = Microsoft.Maui.Controls.Application;
