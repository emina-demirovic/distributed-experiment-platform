using System.Diagnostics;
using Contracts;

namespace Worker.Execution;

public sealed class PythonProcessExperimentExecutor(
    IConfiguration configuration)
    : IExperimentExecutor
{
    private readonly string _pythonCommand =
        configuration["Worker:Executor:PythonCommand"]
        ?? "py";

    private readonly string _scriptPath =
        ResolveScriptPath(
            configuration["Worker:Executor:ScriptPath"]
            ?? "Scripts/run_experiment.py");

    public async Task<ExperimentExecutionResult> ExecuteAsync(
        ExperimentResponse experiment,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_scriptPath))
        {
            return new ExperimentExecutionResult(
                false,
                $"Python script was not found: {_scriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(_scriptPath);

        startInfo.ArgumentList.Add("--experiment-id");
        startInfo.ArgumentList.Add(experiment.Id.ToString());

        startInfo.ArgumentList.Add("--algorithm");
        startInfo.ArgumentList.Add(experiment.Algorithm);

        startInfo.ArgumentList.Add("--environment");
        startInfo.ArgumentList.Add(experiment.Environment);

        startInfo.ArgumentList.Add("--seed");
        startInfo.ArgumentList.Add(experiment.Seed.ToString());

        startInfo.ArgumentList.Add("--max-steps");
        startInfo.ArgumentList.Add(experiment.MaxSteps.ToString());

        if (experiment.SimulateFailure)
        {
            startInfo.ArgumentList.Add("--simulate-failure");
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                return new ExperimentExecutionResult(
                    false,
                    "Python process could not be started.");
            }

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            var standardOutput =
                (await standardOutputTask).Trim();

            var standardError =
                (await standardErrorTask).Trim();

            if (process.ExitCode == 0)
            {
                var message = string.IsNullOrWhiteSpace(
                    standardOutput)
                    ? "Python process completed successfully."
                    : standardOutput;

                return new ExperimentExecutionResult(
                    true,
                    message);
            }

            var errorMessage = string.IsNullOrWhiteSpace(
                standardError)
                ? $"Python process exited with code {process.ExitCode}."
                : standardError;

            return new ExperimentExecutionResult(
                false,
                errorMessage);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ExperimentExecutionResult(
                false,
                $"Python process could not be executed: " +
                exception.Message);
        }
    }

    private static string ResolveScriptPath(
        string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            configuredPath);
    }
}