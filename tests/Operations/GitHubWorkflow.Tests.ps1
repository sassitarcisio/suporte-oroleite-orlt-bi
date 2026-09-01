$workflowDirectory = Join-Path $PSScriptRoot '..\..\.github\workflows'

Describe 'GitHub Actions workflows' {
    It 'does not reference the unavailable azure/setup-azcli action' {
        $workflows = Get-ChildItem $workflowDirectory -Filter '*.yml' | ForEach-Object {
            Get-Content $_.FullName -Raw
        }

        $workflows | Should Not Match 'azure/setup-azcli'
    }
}
