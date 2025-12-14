using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia;
using BruTile.Web;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTouch.UI.ViewModels;

namespace ZeroTouch.UI.Views
{
    public partial class MainDashboardView : UserControl
    {
        private DispatcherTimer? _navigationTimer;

        private List<MPoint> _interpolatedPath = new List<MPoint>();

        private int _currentStepIndex = 0;

        private MemoryLayer? _routeLayer;
        private MemoryLayer? _vehicleLayer;

        private MapControl? _mapControl;

        private double _currentVehicleAngle = 0;

        // 新增一個變數來儲存終點圖層，方便後續更新
        private MemoryLayer? _destinationLayer;
        //用來紀錄目前哪一個方塊被選中
        private Border? _selectedRouteBorder; 

        public MainDashboardView()
        {
            InitializeComponent();

            // This will run in the designer, providing a preview for the MapView.
            InitializeMapView_ForPreview();

            // Runtime initialization is moved to the Loaded event to avoid issues in the designer.
            this.Loaded += MainDashboardView_Loaded;

            var slider = this.FindControl<Slider>("ProgressSlider");
            if (slider != null)
            {
                slider.AddHandler(PointerPressedEvent, OnSliderDragStarted, RoutingStrategies.Tunnel);
                slider.AddHandler(PointerReleasedEvent, OnSliderDragEnded, RoutingStrategies.Tunnel);

                slider.AddHandler(PointerCaptureLostEvent, OnSliderDragEnded, RoutingStrategies.Tunnel);
            }
        }

        private void OnSliderDragStarted(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainDashboardViewModel vm)
            {
                vm.IsUserInteracting = true;
            }
        }

        private void OnSliderDragEnded(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is MainDashboardViewModel vm)
            {
                vm.IsUserInteracting = false;
                vm.SeekCommand.Execute(vm.Progress);
            }
        }

        private void InitializeMap()
        {
            _mapControl = this.FindControl<MapControl>("MapControl");
            if (_mapControl == null) return;

            var map = new Map();

            var urlFormatter = new HttpTileSource(
                new BruTile.Predefined.GlobalSphericalMercator(),
                "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                new[] { "a", "b", "c", "d" },
                name: "CartoDB Voyager"
            );

            map.Layers.Add(new TileLayer(urlFormatter));

            // 初始化空圖層 (這裡先不加資料，等 LoadRoute 填入)
            _routeLayer = new MemoryLayer { Name = "RouteLayer" };
            _vehicleLayer = CreateVehicleLayer();
            _destinationLayer = new MemoryLayer { Name = "DestinationLayer" }; // 新增這行
            
            map.Layers.Add(_routeLayer);
            map.Layers.Add(_destinationLayer);
            map.Layers.Add(_vehicleLayer);

            // 移除預設小工具
            while (map.Widgets.TryDequeue(out _)) { }

            _mapControl.Map = map;

            // 確保載入後執行預設路徑 (Home)
            _mapControl.Loaded += (s, e) =>
            {
                // 1. 設定您想要的「預設起始座標」(這裡示範設為 Home 的起點)
                double startLon = 120.2846; 
                double startLat = 22.7322;
                
                // 轉成地圖座標 (SphericalMercator)
                var p = SphericalMercator.FromLonLat(startLon, startLat);
                var startPoint = new MPoint(p.x, p.y);

                // 2. 將地圖中心對準這裡
                if (_mapControl?.Map?.Navigator != null)
                {
                    _mapControl.Map.Navigator.CenterOn(startPoint);
                    _mapControl.Map.Navigator.ZoomTo(2.0); // 設定縮放層級
                }

                // 3. 預載入 Home 路徑的預覽
                PreviewRoute("Home");

                // 4. 重要：確保計時器是停止的 (不要自動開始跑)
                _navigationTimer?.Stop();
            };
        }

        // 1. 獨立的讀檔方法：只負責回傳座標，不負責畫圖
        private List<MPoint> GetRoutePoints(string routeIdentifier)
        {
            string fileName;
            switch (routeIdentifier)
            {
                case "Home": fileName = "route-1.json"; break;
                case "Work": fileName = "route-2.json"; break;
                default:
                    if (routeIdentifier.StartsWith("route"))
                        fileName = routeIdentifier.EndsWith(".json") ? routeIdentifier : $"{routeIdentifier}.json";
                    else
                        fileName = $"route-{routeIdentifier}.json"; 
                    break;
            }

            var routeUri = new Uri($"avares://ZeroTouch.UI/Assets/Routes/{fileName}");
            var points = new List<MPoint>();

            try
            {
                if (AssetLoader.Exists(routeUri))
                {
                    using var stream = AssetLoader.Open(routeUri);
                    using var reader = new System.IO.StreamReader(stream);
                    var jsonContent = reader.ReadToEnd();
                    using (var doc = System.Text.Json.JsonDocument.Parse(jsonContent))
                    {
                        foreach (var element in doc.RootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("lon", out var lonProp) && 
                                element.TryGetProperty("lat", out var latProp))
                            {
                                var p = SphericalMercator.FromLonLat(lonProp.GetDouble(), latProp.GetDouble());
                                points.Add(new MPoint(p.x, p.y));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load route: {ex.Message}");
            }
            return points;
        }

        // 2. 預覽：只更新右邊的靜態地圖 (MapViewMapControl)
        private void PreviewRoute(string routeIdentifier)
        {
            var originalWaypoints = GetRoutePoints(routeIdentifier);
            if (originalWaypoints.Count < 2) return;

            var previewPath = InterpolatePath(originalWaypoints, stepSize: 1.2);
            
            // 找到右邊的地圖控制項
            var previewMapControl = this.FindControl<MapControl>("MapViewMapControl");
            if (previewMapControl?.Map == null) return;

            // 清除舊圖層，重新繪製
            previewMapControl.Map.Layers.Clear();
            
            // 補回底圖
            previewMapControl.Map.Layers.Add(new TileLayer(new HttpTileSource(
                new BruTile.Predefined.GlobalSphericalMercator(),
                "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png",
                new[] { "a", "b", "c", "d" },
                name: "CartoDB Voyager")));

            // 加入路徑線
            var routeLayer = CreateRouteLayer(previewPath);
            previewMapControl.Map.Layers.Add(routeLayer);
            
            // 加入終點
            previewMapControl.Map.Layers.Add(CreateDestinationLayer(previewPath.Last()));

            // 自動縮放視角以涵蓋整條路徑
            if (routeLayer.Extent != null)
            {
                // Grow(200) 讓視角留點邊距
                previewMapControl.Map.Navigator.ZoomToBox(routeLayer.Extent.Grow(200)); 
            }
            
            previewMapControl.RefreshGraphics();
        }

        // 3. 導航方法 (取代原本的 LoadRoute)：負責主畫面動畫與頁面跳轉
        private void StartNavigation(string routeIdentifier)
        {
            _navigationTimer?.Stop();

            var originalWaypoints = GetRoutePoints(routeIdentifier);
            if (originalWaypoints.Count < 2) return;

            // 設定全域路徑變數 (給 Timer 動畫用)
            _interpolatedPath = InterpolatePath(originalWaypoints, stepSize: 1.2);
            _currentStepIndex = 0;
            _currentVehicleAngle = 0;

            // 更新主畫面地圖圖層
            if (_routeLayer != null)
            {
                var newRouteLayer = CreateRouteLayer(_interpolatedPath);
                _routeLayer.Features = newRouteLayer.Features;
                _routeLayer.DataHasChanged();
            }
            if (_destinationLayer != null)
            {
                var newDestLayer = CreateDestinationLayer(_interpolatedPath.Last());
                _destinationLayer.Features = newDestLayer.Features;
                _destinationLayer.DataHasChanged();
            }

            // 將主地圖視角移到起點
            if (_mapControl?.Map?.Navigator != null)
            {
                _mapControl.Map.Navigator.CenterOn(_interpolatedPath[0]);
                _mapControl.Map.Navigator.ZoomTo(2.0);
            }

            // 開始導航模擬
            StartNavigationSimulation();

            // === 關鍵：切換回主畫面 ===
            if (DataContext is MainDashboardViewModel vm)
            {
                vm.CurrentPageIndex = 1; // 切換到 Dashboard 頁面
            }
        }

        // 4. [新增] 事件處理：滑鼠滑入時預覽 (Hover)
        // 請記得去 XAML 的 Border 加上 PointerEntered="OnRoutePreviewPointerEntered"
        private void OnRoutePreviewPointerEntered(object? sender, PointerEventArgs e)
        {
            // 從 Tag 取得路徑名稱
            if (sender is Control control && control.Tag is string routeName)
            {
                PreviewRoute(routeName);
            }
        }

        // 5. 點擊 Go 按鈕時開始導航
        private void OnGoRouteClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string routeName)
            {
                StartNavigation(routeName);
            }
        }
        
        // [新增/修改] 點擊路徑方塊的事件
        private void OnRouteBlockClicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border clickedBorder && clickedBorder.Tag is string routeName)
            {
                // A. 恢復上一個方塊的顏色
                if (_selectedRouteBorder != null)
                {
                    // 使用完整名稱 Avalonia.Media.SolidColorBrush
                    _selectedRouteBorder.Background = Avalonia.Media.SolidColorBrush.Parse("#252525");
                    
                    // 使用完整名稱 Avalonia.Media.Brushes
                    _selectedRouteBorder.BorderBrush = Avalonia.Media.Brushes.Transparent;
                    
                    _selectedRouteBorder.BorderThickness = new Thickness(0);
                }

                // B. 設定新方塊的顏色
                _selectedRouteBorder = clickedBorder;
                
                // 設定選中時的背景色 (深灰色)
                _selectedRouteBorder.Background = Avalonia.Media.SolidColorBrush.Parse("#383838");
                
                // 設定選中時的邊框色 (綠色)
                _selectedRouteBorder.BorderBrush = Avalonia.Media.SolidColorBrush.Parse("#2ECC71");
                
                _selectedRouteBorder.BorderThickness = new Thickness(2);

                // C. 呼叫預覽
                PreviewRoute(routeName);
            }
        }

        private List<MPoint> InterpolatePath(List<MPoint> waypoints, double stepSize)
        {
            var result = new List<MPoint>();

            if (waypoints == null || waypoints.Count == 0) return result;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var start = waypoints[i];
                var end = waypoints[i + 1];

                var distance = start.Distance(end);

                // Count steps needed
                var steps = Math.Max(1, (int)(distance / stepSize));

                // Linear interpolation
                for (int j = 0; j < steps; j++)
                {
                    var fraction = (double)j / steps;
                    var x = start.X + (end.X - start.X) * fraction;
                    var y = start.Y + (end.Y - start.Y) * fraction;
                    result.Add(new MPoint(x, y));
                }
            }

            // add the last waypoint
            result.Add(waypoints.Last());

            return result;
        }

        private MemoryLayer CreateRouteLayer(List<MPoint> pathPoints)
        {
            if (pathPoints == null || pathPoints.Count < 2) return new MemoryLayer { Name = "RouteLayer" };

            var coordinates = pathPoints.Select(p => new Coordinate(p.X, p.Y)).ToArray();

            var lineString = new LineString(coordinates);

            var feature = new GeometryFeature
            {
                Geometry = lineString
            };

            feature.Styles.Add(new VectorStyle
            {
                Line = new Pen(Color.FromArgb(200, 33, 150, 243), 3) // Blue
            });

            return new MemoryLayer
            {
                Name = "RouteLayer",
                Features = new[] { feature }
            };
        }

        private MemoryLayer CreateDestinationLayer(MPoint endPoint)
        {
            var pointFeature = new GeometryFeature
            {
                Geometry = new NetTopologySuite.Geometries.Point(endPoint.X, endPoint.Y)
            };

            pointFeature.Styles.Add(new SymbolStyle
            {
                Fill = new Brush(Color.Gold),
                Outline = new Pen(Color.White, 3),
                SymbolScale = 0.8,
                SymbolType = SymbolType.Ellipse
            });

            return new MemoryLayer { Name = "DestinationLayer", Features = new[] { pointFeature } };
        }

        private MemoryLayer CreateVehicleLayer()
        {
            return new MemoryLayer { Name = "VehicleLayer" };
        }

        private void StartNavigationSimulation()
        {
            if (_mapControl?.Map?.Navigator == null) return;

            _mapControl.Map.Navigator.CenterOn(_interpolatedPath[0]);
            _mapControl.Map.Navigator.ZoomTo(2.0);

            _navigationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };

            double currentSmoothRotation = _mapControl.Map.Navigator.Viewport.Rotation;

            _navigationTimer.Tick += (s, e) =>
            {
                _currentStepIndex++;

                if (_currentStepIndex >= _interpolatedPath.Count)
                {
                    _currentStepIndex = 1;  // Loop back to start
                    currentSmoothRotation = 0;
                    _currentVehicleAngle = 0;
                    _mapControl.Map.Navigator.RotateTo(0, duration: 0);
                }

                var newLocation = _interpolatedPath[_currentStepIndex];

                var prevIndex = Math.Max(0, _currentStepIndex - 1);

                if (prevIndex < 0) prevIndex = 0;

                var prevLocation = _interpolatedPath[prevIndex];

                var dx = newLocation.X - prevLocation.X;
                var dy = newLocation.Y - prevLocation.Y;
                double angleDeg = 0;

                if (Math.Abs(dx) > 0.0001 || Math.Abs(dy) > 0.0001)
                {
                    var angleRad = Math.Atan2(dy, dx);
                    angleDeg = angleRad * 180.0 / Math.PI;

                    var targetRotation = angleDeg - 90;

                    currentSmoothRotation = LerpAngle(currentSmoothRotation, targetRotation, 0.1);

                    _mapControl.Map.Navigator.RotateTo(currentSmoothRotation, duration: 0);

                    _currentVehicleAngle = LerpAngle(_currentVehicleAngle, angleDeg, 0.1);
                }

                if (_routeLayer != null)
                {
                    var remainingPoints = _interpolatedPath.Skip(_currentStepIndex).ToList();

                    if (remainingPoints.Count >= 2)
                    {
                        var coords = remainingPoints.Select(p => new Coordinate(p.X, p.Y)).ToArray();
                        var newLineString = new LineString(coords);

                        var newRouteFeature = new GeometryFeature { Geometry = newLineString };

                        newRouteFeature.Styles.Add(new VectorStyle
                        {
                            Line = new Pen(Color.FromArgb(200, 33, 150, 243), 6)
                        });

                        _routeLayer.Features = new[] { newRouteFeature };
                        _routeLayer.DataHasChanged();
                    }
                    else
                    {
                        // Final point reached, clear the route
                        _routeLayer.Features = new List<IFeature>();
                        _routeLayer.DataHasChanged();
                    }
                }

                if (_vehicleLayer != null)
                {
                    var arrowPolygon = CreateArrowPolygon(newLocation, _currentVehicleAngle);

                    var newFeature = new GeometryFeature
                    {
                        Geometry = arrowPolygon
                    };

                    var oldFeature = _vehicleLayer.Features.FirstOrDefault();

                    if (oldFeature?.Styles.FirstOrDefault() is IStyle oldStyle)
                    {
                        newFeature.Styles.Add(oldStyle);
                    }
                    else
                    {
                        newFeature.Styles.Add(new VectorStyle
                        {
                            Fill = new Brush(Color.Red),
                            Outline = new Pen(Color.White, 2)
                        });
                    }

                    _vehicleLayer.Features = new[] { newFeature };
                    _vehicleLayer.DataHasChanged();
                }

                // Center map on the new location
                _mapControl.Map.Navigator.CenterOn(newLocation);

                // Rotate the map slightly for effect
                // _mapControl.Map.Navigator.RotateTo(_mapControl.Map.Navigator.Viewport.Rotation + 0.01);
                
                // ==========================================
                // [新增] 計算距離並更新 ViewModel
                // ==========================================
                if (DataContext is MainDashboardViewModel vm)
                {
                    // 1. 計算距離下一個轉彎的距離
                    double distToTurn = GetDistanceToNextTurn(_currentStepIndex);
                    
                    // 2. 計算總剩餘距離 (用來判斷是否快到終點)
                    double distToDest = CalculateRemainingDistance(_currentStepIndex); // 這是上一輪教的方法，保留著用

                    // 3. 判斷邏輯
                    if (distToDest < 30) 
                    {
                        // 如果離總終點小於 20 公尺
                        vm.NavigationDistance = "Arriving";
                        vm.NavigationInstruction = "Destination";
                        vm.NavigationIcon = "●";
                    }
                    else
                    {
                        // 更新距離顯示
                        if (distToTurn > 1000)
                            vm.NavigationDistance = $"in {(distToTurn / 1000.0):F1} km";
                        else
                            vm.NavigationDistance = $"in {(int)distToTurn} m";

                        // 更新轉彎指令 (這部分結合上一輪的轉彎判斷)
                        UpdateTurnInstruction(vm); 
                        
                        // 優化顯示：如果是直線且距離很長
                        if (vm.NavigationIcon == "↑") 
                        {
                            // 直行時，通常顯示 "Go Straight" 搭配 "距離下個轉彎"
                            // 這樣使用者就知道要直走多久
                        }
                    }
                }
                // ==========================================
                _mapControl.RefreshGraphics();
            };

            _navigationTimer.Start();
        }

        private Polygon CreateArrowPolygon(MPoint center, double angleDegrees)
        {
            double scale = 3.0;

            var points = new[]
            {
                new MPoint(0, 15),
                new MPoint(10, -10),
                new MPoint(0, -4),
                new MPoint(-10, -10),
                new MPoint(0, 15)
            };

            double rad = (angleDegrees - 90) * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            var rotatedCoordinates = new Coordinate[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];

                double xScaled = p.X * scale;
                double yScaled = p.Y * scale;

                double xRot = (xScaled * cos) - (yScaled * sin);
                double yRot = (xScaled * sin) + (yScaled * cos);

                rotatedCoordinates[i] = new Coordinate(center.X + xRot, center.Y + yRot);
            }

            return new Polygon(new LinearRing(rotatedCoordinates));
        }

        private void MainDashboardView_Loaded(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= MainDashboardView_Loaded; // Prevent multiple calls
            InitializeMap(); // Initialize the main dashboard map at runtime
        }

        private void InitializeMapView_ForPreview()
        {
            try
            {
                var mapViewMapControl = this.FindControl<MapControl>("MapViewMapControl");
                if (mapViewMapControl != null)
                {
                    var map = new Map();
                    map.Layers.Add(new TileLayer(new HttpTileSource(new BruTile.Predefined.GlobalSphericalMercator(), "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png", new[] { "a", "b", "c", "d" }, name: "CartoDB Voyager")));

                    (double, double)[] lonLats;
                    var routeUri = new Uri("avares://ZeroTouch.UI/Assets/Routes/route-1.json");

                    if (AssetLoader.Exists(routeUri))
                    {
                        using var stream = AssetLoader.Open(routeUri);
                        using var reader = new System.IO.StreamReader(stream);
                        var jsonContent = reader.ReadToEnd();

                        using (var doc = System.Text.Json.JsonDocument.Parse(jsonContent))
                        {
                            lonLats = doc.RootElement.EnumerateArray()
                                .Select(p => (p.GetProperty("lon").GetDouble(), p.GetProperty("lat").GetDouble()))
                                .ToArray();
                        }
                    }
                    else
                    {
                        // Fallback to hardcoded data if file not found in designer
                        lonLats = new[]
                        {
                            (0.0,0.0)
                        };
                    }

                    var originalWaypoints = new List<MPoint>();
                    foreach (var (lon, lat) in lonLats)
                    {
                        var p = SphericalMercator.FromLonLat(lon, lat);
                        originalWaypoints.Add(new MPoint(p.x, p.y));
                    }

                    var interpolatedPath = InterpolatePath(originalWaypoints, stepSize: 1.2);
                    if (interpolatedPath.Any())
                    {
                        var routeLayer = CreateRouteLayer(interpolatedPath);
                        map.Layers.Add(routeLayer);

                        var destLayer = CreateDestinationLayer(interpolatedPath.Last());
                        map.Layers.Add(destLayer);

                        mapViewMapControl.Map = map;
                        if (routeLayer.Extent is not null)
                        {
                            mapViewMapControl.Map.Navigator.ZoomToBox(routeLayer.Extent.Grow(100));
                        }
                    }
                    else
                    {
                        mapViewMapControl.Map = map;
                        var center = SphericalMercator.FromLonLat(120.29, 22.72);
                        mapViewMapControl.Map.Navigator.CenterOn(new MPoint(center.x, center.y));
                        mapViewMapControl.Map.Navigator.ZoomTo(14);
                    }
                }
            }
            catch
            {
                // Ignore exceptions in designer
            }
        }

        private double LerpAngle(double current, double target, double t)
        {
            double diff = target - current;

            while (diff > 180) diff -= 360;
            while (diff < -180) diff += 360;

            return current + diff * t;
        }
    
        // 在 class 內新增一個計算剩餘距離的方法
        private double CalculateRemainingDistance(int currentIndex)
        {
            double totalDistance = 0;
            
            // 從當前索引開始，累加到最後一個點的距離
            // 注意：這是一個簡單的估算，實際導航通常只算到「下一個轉彎點」
            // 但因為你的路徑是插值過的(interpolated)，點非常密，這樣累加會得到總剩餘距離
            for (int i = currentIndex; i < _interpolatedPath.Count - 1; i++)
            {
                totalDistance += _interpolatedPath[i].Distance(_interpolatedPath[i + 1]);
            }
            
            return totalDistance;
        }
    
        // 放在 MainDashboardView 類別內   
        private void UpdateTurnInstruction(MainDashboardViewModel vm)
        {
            // 1. 設定「前瞻距離」：往後看約 80 個點 (因為 stepSize=1.2，約 48 公尺)
            int lookAheadSteps = 80; 
            
            // 如果快到終點了，就不判斷了
            if (_currentStepIndex + lookAheadSteps + 1 >= _interpolatedPath.Count)
            {
                vm.NavigationInstruction = "Arriving";
                vm.NavigationIcon = "●"; // 終點圖示
                return;
            }

            // 2. 取得「當前向量」 (車子現在的方向)
            var pNow = _interpolatedPath[_currentStepIndex];
            var pNext = _interpolatedPath[_currentStepIndex + 1];
            double dx1 = pNext.X - pNow.X;
            double dy1 = pNext.Y - pNow.Y;
            double angleCurrent = Math.Atan2(dy1, dx1); // 弧度

            // 3. 取得「未來向量」 (前方路段的方向)
            var pFuture = _interpolatedPath[_currentStepIndex + lookAheadSteps];
            var pFutureNext = _interpolatedPath[_currentStepIndex + lookAheadSteps + 1];
            double dx2 = pFutureNext.X - pFuture.X;
            double dy2 = pFutureNext.Y - pFuture.Y;
            double angleFuture = Math.Atan2(dy2, dx2); // 弧度

            // 4. 計算角度差 (將差異限制在 -PI 到 +PI 之間)
            double diff = angleFuture - angleCurrent;
            while (diff > Math.PI) diff -= 2 * Math.PI;
            while (diff < -Math.PI) diff += 2 * Math.PI;

            // 5. 設定閾值 (例如 20度 ≒ 0.35 弧度) 來判斷是否轉彎
            // 正值代表左轉 (逆時針)，負值代表右轉 (順時針) - 這是數學上的定義
            // 但在地圖座標(Y向上)中：
            // 如果車向東(0)，未來向北(90度)，差+90 => 左轉
            // 如果車向東(0)，未來向南(-90度)，差-90 => 右轉

            double turnThreshold = 0.35; // 約 20 度

            if (diff > turnThreshold)
            {
                vm.NavigationInstruction = "Turn Left";
                vm.NavigationIcon = "↰";
            }
            else if (diff < -turnThreshold)
            {
                vm.NavigationInstruction = "Turn Right";
                vm.NavigationIcon = "↱";
            }
            else
            {
                // 角度變化不大，顯示直行
                vm.NavigationInstruction = "Go Straight";
                vm.NavigationIcon = "↑";
            }
        }
    
        // 鎖定車子目前的行進角度，然後往後檢查路徑，一旦發現某個點的角度與起始角度偏差超過一定數值（例如 25 度），就認定那是轉彎點。
        private double GetDistanceToNextTurn(int currentIndex)
        {
            // 如果已經到終點，距離為 0
            if (currentIndex >= _interpolatedPath.Count - 1) return 0;

            double accumulatedDistance = 0;
            
            // 1. 取得當前車子行進的基準角度 (Start Angle)
            var pCurrent = _interpolatedPath[currentIndex];
            var pNext = _interpolatedPath[currentIndex + 1];
            double baseAngle = Math.Atan2(pNext.Y - pCurrent.Y, pNext.X - pCurrent.X);

            // 2. 往後掃描路徑
            for (int i = currentIndex; i < _interpolatedPath.Count - 1; i++)
            {
                var p1 = _interpolatedPath[i];
                var p2 = _interpolatedPath[i + 1];

                // 累加每一小段的距離
                double segmentDist = p1.Distance(p2);
                accumulatedDistance += segmentDist;

                // 3. 計算掃描到的路段角度
                double scanAngle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

                // 4. 計算與基準角度的差異 (處理 -PI 到 +PI 的循環)
                double diff = scanAngle - baseAngle;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff < -Math.PI) diff += 2 * Math.PI;

                // 5. 設定閾值：如果角度偏差超過 25 度 (約 0.43 弧度)，視為轉彎點
                if (Math.Abs(diff) > 0.43)
                {
                    return accumulatedDistance;
                }
            }

            // 如果一路到底都沒有大轉彎，就回傳到終點的距離
            return accumulatedDistance;
        }
    }
}
