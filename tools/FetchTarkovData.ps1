# PowerShell script to fetch Tarkov data from tarkov.dev API and save as DEFAULT_DATA.json
# Run with: .\FetchTarkovData.ps1

Write-Host "Fetching Tarkov data from tarkov.dev API..."

# Updated query to match new tarkov.dev GraphQL schema (uses union types for objectives)
$query = @"
{
    maps {
        name
        nameId
        extracts {
            name
            faction
            position {x,y,z}
        }
        transits {
            description
            position {x,y,z}
        }
    }
    items { 
        id 
        name 
        shortName 
        width 
        height 
        sellFor { 
            vendor { 
                name 
            } 
            priceRUB 
        } 
        basePrice 
        avg24hPrice 
        historicalPrices { 
            price 
        } 
        categories { 
            name 
        } 
    }
    lootContainers { 
        id 
        normalizedName 
        name 
    }
    tasks {
        id
        tarkovDataId
        name
        trader {
            name
        }
        map {
            nameId
            name
        }
        objectives {
            id
            type
            description
            maps {
                nameId
                name
            }
            ... on TaskObjectiveBasic {
                zones {
                    id
                    map {
                        nameId
                        name
                    }
                    position {x,y,z}
                }
            }
            ... on TaskObjectiveItem {
                count
                foundInRaid
                item {
                    id
                    name
                    shortName
                }
                zones {
                    id
                    map {
                        nameId
                        name
                    }
                    position {x,y,z}
                }
            }
            ... on TaskObjectiveQuestItem {
                questItem {
                    id
                    name
                    shortName
                    normalizedName
                    description
                }
                zones {
                    id
                    map {
                        nameId
                        name
                    }
                    position {x,y,z}
                }
            }
            ... on TaskObjectiveMark {
                markerItem {
                    id
                    name
                    shortName
                }
                zones {
                    id
                    map {
                        nameId
                        name
                    }
                    position {x,y,z}
                }
            }
            ... on TaskObjectiveShoot {
                count
                zones {
                    id
                    map {
                        nameId
                        name
                    }
                    position {x,y,z}
                }
            }
            ... on TaskObjectiveExtract {
                count
            }
            ... on TaskObjectiveBuildItem {
                item {
                    id
                    name
                    shortName
                }
            }
        }
        neededKeys {
            keys {
                id
                name
                shortName
            }
            map {
                nameId
                name
            }
        }
    }
}
"@

$body = @{ query = $query } | ConvertTo-Json -Compress

try {
    $response = Invoke-RestMethod -Uri "https://api.tarkov.dev/graphql" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 120
    
    # Debug: Check if we got data
    if ($null -eq $response.data) {
        Write-Host "Error: API returned null data" -ForegroundColor Red
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Yellow
        exit 1
    }
    
    $items = $response.data.items
    $lootContainers = $response.data.lootContainers
    $maps = $response.data.maps
    $tasks = $response.data.tasks
    
    Write-Host "Received: $($items.Count) items, $($tasks.Count) tasks, $($maps.Count) maps, $($lootContainers.Count) containers"
    
    # Transform items to output format
    $outputItems = @()
    
    foreach ($item in $items) {
        if ($null -eq $item) { continue }
        
        $slots = $item.width * $item.height
        
        # Calculate trader price (highest non-flea vendor price)
        $traderPrice = 0
        foreach ($sell in $item.sellFor) {
            if ($sell.vendor.name -ne "Flea Market" -and $sell.priceRUB -gt $traderPrice) {
                $traderPrice = $sell.priceRUB
            }
        }
        
        # Calculate flea price
        $fleaPrice = 0
        if ($item.basePrice -gt 0) {
            if ($item.avg24hPrice -gt 0) {
                $fleaPrice = $item.avg24hPrice
            } elseif ($item.historicalPrices.Count -gt 0) {
                $validPrices = $item.historicalPrices | Where-Object { $_.price -gt 0 } | Select-Object -ExpandProperty price
                if ($validPrices.Count -gt 0) {
                    $fleaPrice = [math]::Round(($validPrices | Measure-Object -Average).Average)
                }
            }
        }
        
        $outputItems += @{
            bsgID = $item.id
            name = $item.name
            shortName = $item.shortName
            price = $traderPrice
            fleaPrice = $fleaPrice
            slots = $slots
            categories = @($item.categories | ForEach-Object { $_.name })
        }
    }
    
    # Add containers
    foreach ($container in $lootContainers) {
        if ($null -eq $container) { continue }
        
        $outputItems += @{
            bsgID = $container.id
            name = $container.normalizedName
            shortName = $container.name
            price = -1
            fleaPrice = -1
            slots = 1
            categories = @("Static Container")
        }
    }
    
    # Build output object
    $output = @{
        items = $outputItems
        maps = $maps
        tasks = $tasks
    }
    
    # Save to file - use UTF8 encoding that works with older PowerShell
    $outputPath = Join-Path $PSScriptRoot "..\Resources\DEFAULT_DATA.json"
    $jsonContent = $output | ConvertTo-Json -Depth 20 -Compress
    [System.IO.File]::WriteAllText($outputPath, $jsonContent, [System.Text.UTF8Encoding]::new($false))
    
    $fileSize = (Get-Item $outputPath).Length / 1024
    Write-Host "Saved to: $outputPath"
    Write-Host "File size: $([math]::Round($fileSize)) KB"
    Write-Host "Done!"
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Yellow
    exit 1
}
