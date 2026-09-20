$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$viewPath = Join-Path $root 'Views\LubeCalculationTabView.xaml'
$xml = New-Object System.Xml.XmlDocument
$xml.Load($viewPath)

function Assert-Ui($condition, [string]$message) {
	if (-not $condition) { throw $message }
	Write-Host "PASS: $message"
}

$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace('p', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
$ns.AddNamespace('views', 'clr-namespace:UmiaPP.Views')

$grids = $xml.SelectNodes('//p:DataGrid[@x:Name="SakvarorSpecificationGrid"]', $ns)
Assert-Ui ($grids.Count -eq 1) 'Exactly one specification grid exists.'
$grid = $grids[0]
$bars = $xml.SelectNodes('//views:SakvaraActionsBar', $ns)
Assert-Ui ($bars.Count -eq 1) 'Exactly one horizontal quote action bar exists.'
$bar = $bars[0]
Assert-Ui ([object]::ReferenceEquals($bar.ParentNode, $grid.ParentNode)) 'The toolbar and specification grid share one layout container.'
Assert-Ui ($bar.GetAttribute('Grid.Row') -eq '0' -and $grid.GetAttribute('Grid.Row') -eq '1') 'Toolbar and table occupy separate rows.'
$rows = $grid.ParentNode.SelectNodes('p:Grid.RowDefinitions/p:RowDefinition', $ns)
Assert-Ui ($rows.Count -ge 3 -and $rows[0].GetAttribute('Height') -eq 'Auto' -and $rows[1].GetAttribute('Height') -eq '*') 'The toolbar has auto height and the table fills remaining space.'
Assert-Ui ($bar.GetAttribute('SelectedArticles') -match 'SelectedItems' -and $bar.GetAttribute('SelectedArticles') -match 'ElementName=SakvarorSpecificationGrid') 'Quote actions receive the actual grid selection.'
$oldActions = $grid.ParentNode.SelectNodes('.//p:Button[contains(@Command,"AddSakvaraRowCommand") or contains(@Command,"LinkSelectedToQuoteCommand")]', $ns)
Assert-Ui ($oldActions.Count -eq 0) 'No obsolete standalone action buttons remain over the new toolbar.'

Assert-Ui ($xml.OuterXml -notmatch 'IsTabPickerOpen') 'The tab picker does not reference the missing view-model property.'
$toggle = $xml.SelectSingleNode('//p:ToggleButton[@x:Name="TabPickerButton"]', $ns)
Assert-Ui ($null -ne $toggle) 'The tab picker uses a ToggleButton.'
$popup = $xml.SelectSingleNode('//p:Popup[contains(@PlacementTarget,"TabPickerButton")]', $ns)
Assert-Ui ($null -ne $popup) 'The popup is anchored to the tab toggle.'
Assert-Ui ($popup.GetAttribute('IsOpen') -match 'IsChecked' -and $popup.GetAttribute('IsOpen') -match 'Mode=TwoWay') 'Popup open state follows the toggle in both directions.'
Assert-Ui ($popup.GetAttribute('DataContext') -match 'PlacementTarget.DataContext') 'The popup obtains the view model through its placement target.'
$options = $popup.SelectSingleNode('.//*[@ItemsSource="{Binding TabOptions}"]', $ns)
Assert-Ui ($null -ne $options) 'The popup lists the actual tab options.'
$check = $options.SelectSingleNode('.//p:CheckBox[contains(@IsChecked,"IsVisible")]', $ns)
Assert-Ui ($null -ne $check -and $check.GetAttribute('IsChecked') -match 'Mode=TwoWay') 'Tab visibility checkboxes update their option models.'

$disabledRows = $xml.SelectNodes('//p:Style[contains(@TargetType,"DataGridRow")]//p:DataTrigger[contains(@Binding,"IsQuoteSummaryRow")]//p:Setter[(@Property="IsEnabled" or @Property="IsHitTestVisible") and @Value="False"]', $ns)
Assert-Ui ($disabledRows.Count -eq 0) 'Quote summary rows can receive double-click input.'
Assert-Ui ($grid.GetAttribute('Loaded') -eq 'SakvarorGrid_Loaded') 'Grid loading attaches the quote interaction handlers.'
Assert-Ui ($grid.GetAttribute('BeginningEdit') -eq 'SakvarorGrid_BeginningEdit') 'Summary row editing remains guarded.'
$code = [System.IO.File]::ReadAllText((Join-Path $root 'Views\LubeCalculationTabView.xaml.cs'))
Assert-Ui ($code -match 'AttachQuoteInteractions\(specificationGrid\)') 'The loaded handler attaches the implemented interactions.'

Write-Host 'Source checks complete. A rebuild and runtime testing are still required.'
