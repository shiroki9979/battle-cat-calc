"""測試跨池路徑規劃功能。

動機：驗證 GachaSimulator 能正確處理多池場景，包括：
1. 多池初始化與分支展開
2. 跨池路徑規劃（不同池有不同目標）
3. 重複稀有跨池避免
"""
import pytest
from battle_cat_calc.simulator import GachaSimulator
from battle_cat_calc.models import Track, GachaSlot, Position, GachaPool


class TestMultiPoolSimulator:
    """測試多池模擬器核心功能。"""

    def test_multi_pool_initialization(self):
        """測試多池模式初始化。
        
        動機：確保模擬器能正確接受 List[GachaPool] 並建立 pools 字典。
        方法：建立兩個 GachaPool，傳入模擬器，驗證 pools 字典正確。
        """
        pool_a = GachaPool(
            pool_id="ultra_souls",
            name="超靈魂祭",
            track_a=Track("A", [GachaSlot(id=Position(1, 'A'), result="Jizo", guaranteed="G1")]),
            track_b=Track("B", []),
            has_guaranteed=True
        )
        pool_b = GachaPool(
            pool_id="epicfest",
            name="極祭典",
            track_a=Track("A", [GachaSlot(id=Position(1, 'A'), result="Mitama", guaranteed="G2")]),
            track_b=Track("B", []),
            has_guaranteed=False
        )
        
        sim = GachaSimulator([pool_a, pool_b])
        
        assert sim.is_multi_pool is True
        assert len(sim.pools) == 2
        assert "ultra_souls" in sim.pools
        assert "epicfest" in sim.pools

    def test_cross_pool_path_planning(self):
        """測試跨池路徑規劃。
        
        動機：驗證模擬器能在不同池之間做出最佳選擇。
        方法：設定目標「Jizo」只在池 A 的第 2 格，模擬器應選擇池 A。
        """
        # 池 A: 1A=Cat1, 2A=Jizo
        pool_a = GachaPool(
            pool_id="pool_a",
            name="Pool A",
            track_a=Track("A", [
                GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="G1"),
                GachaSlot(id=Position(2, 'A'), result="Jizo", guaranteed="G2"),
            ]),
            track_b=Track("B", []),
            has_guaranteed=True
        )
        # 池 B: 1A=CatX, 2A=CatY (沒有 Jizo)
        pool_b = GachaPool(
            pool_id="pool_b",
            name="Pool B",
            track_a=Track("A", [
                GachaSlot(id=Position(1, 'A'), result="CatX", guaranteed="G1"),
                GachaSlot(id=Position(2, 'A'), result="CatY", guaranteed="G2"),
            ]),
            track_b=Track("B", []),
            has_guaranteed=True
        )
        
        sim = GachaSimulator([pool_a, pool_b])
        result = sim.find_best_path(Position(1, 'A'), {"Jizo"}, 10)
        
        assert result is not None
        assert result.total_cost == 2
        # 驗證路徑使用了 pool_a
        assert any(step.pool_id == "pool_a" for step in result.path)

    def test_pool_id_in_step(self):
        """測試 GachaStep 正確記錄 pool_id。
        
        動機：跨池規劃需要知道每一步在哪個池抽。
        方法：執行模擬，檢查每個 step 的 pool_id 欄位。
        """
        pool = GachaPool(
            pool_id="test_pool",
            name="Test Pool",
            track_a=Track("A", [
                GachaSlot(id=Position(1, 'A'), result="Target", guaranteed="G"),
            ]),
            track_b=Track("B", []),
            has_guaranteed=True
        )
        
        sim = GachaSimulator([pool])
        result = sim.find_best_path(Position(1, 'A'), {"Target"}, 5)
        
        assert result is not None
        step = result.path[0]
        assert step.pool_id == "test_pool"

    def test_backward_compatibility_single_pool(self):
        """測試單池模式向後相容性。
        
        動機：確保舊有的 (track_a, track_b) 呼叫方式仍然有效。
        方法：使用舊式 API 初始化，驗證功能正常。
        """
        track_a = Track("A", [
            GachaSlot(id=Position(1, 'A'), result="Cat1", guaranteed="G1"),
        ])
        track_b = Track("B", [])
        
        sim = GachaSimulator(track_a, track_b)
        
        assert sim.is_multi_pool is False
        assert "default" in sim.pools
        
        result = sim.find_best_path(Position(1, 'A'), {"Cat1"}, 5)
        assert result is not None
        assert result.total_cost == 1

    def test_pool_without_guaranteed(self):
        """測試沒有必中選項的池子。
        
        動機：某些池（如極祭典）沒有必中選項，模擬器應跳過保底分支。
        方法：建立 has_guaranteed=False 的池，驗證只產生單抽路徑。
        """
        pool = GachaPool(
            pool_id="no_gua_pool",
            name="No Guaranteed Pool",
            track_a=Track("A", [
                GachaSlot(id=Position(i, 'A'), result=f"Cat{i}", guaranteed="G", next_pos=Position(i+10, 'B'))
                for i in range(1, 15)
            ]),
            track_b=Track("B", []),
            has_guaranteed=False
        )
        
        sim = GachaSimulator([pool])
        # 目標在第 5 格，沒有保底選項，只能單抽
        result = sim.find_best_path(Position(1, 'A'), {"Cat5"}, 10)
        
        assert result is not None
        assert result.total_cost == 5
        # 所有步驟都應該是單抽
        assert all(not step.is_guaranteed for step in result.path)
