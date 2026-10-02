import re
from bs4 import BeautifulSoup, Tag
from typing import Dict, List, NamedTuple, Optional, Sequence, Set, Tuple, Union
from .models import CatRarity, GachaSlot, Position, ParseStatus, Track


class _ColsData(NamedTuple):
    """單列內「結果欄 + 必中欄」解析出的原始資料。"""
    result: str
    guaranteed: str
    result_pos: Optional[Position]      # 結果欄的箭頭（重複稀有的單抽落點）
    guaranteed_pos: Optional[Position]  # 必中欄的箭頭（必中十一連的落點）
    original_result: Optional[str]
    is_duplicate: bool
    original_guaranteed: Optional[str]


class BCParser:
    """將 BC Godfat 的 HTML 內容解析為結構化資料。"""
    
    def __init__(self, html_content: str):
        self.html_content = html_content
        self.regex_a = r'^N([0-9]+)(A)$'
        self.regex_b = r'^N([0-9]+)(B)$'
        self.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'
        self.track_a, self.track_b = self._parse_gacha_table()

    def _parse_gacha_table(self) -> Tuple[Track, Track]:
        """解析轉蛋表格並回傳 Track A 與 Track B 的 tuple。"""
        soup = BeautifulSoup(self.html_content, "html.parser")
        table = soup.find("table")
        if not table:
            raise ValueError("無法解析 HTML: 找不到轉蛋表格")

        rows = table.find_all("tr")
        # 重複稀有格會讓相鄰儲存格以 rowspan 互相重疊，同一列的第 N 個 <td>
        # 不一定是第 N 欄，必須先還原網格才能正確歸屬儲存格。
        self._grid, self._cell_cols = self._build_cell_grid(rows)

        track_a = self._parse_track_a(rows)
        track_b = self._parse_track_b(rows)

        # Validate tracks
        if not self._validate_track(track_a) or not self._validate_track(track_b):
            # If validation fails, we can return empty tracks or 
            # perhaps the partially parsed tracks depending on requirements.
            # For now, let's keep it simple as per the plan.
            raise ValueError("Invalid track")

        rarities = self._read_cat_rarities(soup)
        for track in (track_a, track_b):
            for slot in track:
                slot.rarity = rarities.get(slot.result) if isinstance(slot.result, str) else None
                slot.guaranteed_rarity = rarities.get(slot.guaranteed) if isinstance(slot.guaranteed, str) else None

        return (track_a, track_b)

    def _read_cat_rarities(self, soup: BeautifulSoup) -> Dict[str, CatRarity]:
        """從頁面的貓咪目錄（#find_select）讀取每隻貓的稀有度。

        表格儲存格的 CSS class（rare / supa_fest ...）是 godfat 依慶典費率上色的「稀有度帶」，
        與貓本身的稀有度不一致（例如拳擊手貓是 Rare，class 卻是 supa_fest），
        遊戲規則的判定必須用目錄。
        """
        labels = {
            "Rare": CatRarity.RARE,
            "Super": CatRarity.SUPER_RARE,
            "Uber": CatRarity.UBER_RARE,
            "Legendary": CatRarity.LEGEND,
        }
        rarities: Dict[str, CatRarity] = {}
        for group in soup.select("#find_select optgroup"):
            rarity = labels.get(group.get("label", "").strip().rstrip(":"))
            if rarity is None:
                continue
            for option in group.select("option"):
                rarities[option.get_text(strip=True)] = rarity
        return rarities

    def _parse_track_a(self, rows: List) -> Track:
        """掃描並解析 Track A 上的所有格子。"""
        slots = []
        row_index = 0
        while row_index < len(rows):
            row = rows[row_index]
            tds = row.find_all("td")
            if not tds:
                row_index += 1
                continue

            # Check for Track A start
            html_id_a = tds[0].get("id", "")
            match_a = re.match(self.regex_a, html_id_a)
            
            if match_a:
                slot = self._parse_slot_a(rows, row_index, tds[0])
                slots.append(slot)
                row_index += int(tds[0].get("rowspan", 1))
            else:
                row_index += 1
        return Track("A", slots)

    def _parse_track_b(self, rows: List) -> Track:
        """掃描並解析 Track B 上的所有格子。"""
        slots = []
        row_index = 0
        while row_index < len(rows):
            row = rows[row_index]
            tds = row.find_all("td")
            if not tds:
                row_index += 1
                continue

            # Check for Track B start (usually at the end of the row)
            html_id_b = tds[-1].get("id", "")
            match_b = re.match(self.regex_b, html_id_b)

            if match_b:
                slot = self._parse_slot_b(rows, row_index, tds[-1])
                slots.append(slot)
                row_index += int(tds[-1].get("rowspan", 1))
            else:
                row_index += 1
        return Track("B", slots)

    def _parse_slot_a(self, all_rows, start_idx, id_td) -> GachaSlot:
        """解析 Track A 單一格子的資料。"""
        return self._parse_slot(all_rows, start_idx, id_td, track='A')

    def _parse_slot_b(self, all_rows: List, start_idx: int, id_td: Tag) -> GachaSlot:
        """解析 Track B 單一格子的資料。"""
        return self._parse_slot(all_rows, start_idx, id_td, track='B')

    def _parse_slot(self, all_rows: List, start_idx: int, id_td: Tag, track: str) -> GachaSlot:
        """統一解析單一格子的資料（適用於 Track A 與 Track B）。
        
        Args:
            all_rows: 所有表格列。
            start_idx: 起始列索引。
            id_td: 包含格子 ID 的 <td> 元素。
            track: 軌道類型 ('A' 或 'B')。
        
        Returns:
            解析後的 GachaSlot 物件。
        """
        regex = self.regex_a if track == 'A' else self.regex_b
        match = re.match(regex, id_td.get("id", ""))
        slot_id = (int(match.group(1)), match.group(2))
        rowspan = int(id_td.get("rowspan", 1))

        # A 軌的結果/必中欄緊接在 id 右側；B 軌（Alt. 欄）則在 id 左側。
        id_col = self._cell_cols[id(id_td)]
        if track == 'A':
            res_col, gua_col = id_col + 1, id_col + 2
        else:
            res_col, gua_col = id_col - 2, id_col - 1

        all_results = []
        all_gua = []
        is_dup_flag = False
        result_pos = None
        gua_arrows: List[Position] = []  # 依列順序：重複稀有格的第一個是替換前、最後一個是替換後

        for i in range(rowspan):
            row_idx = start_idx + i
            cols = [self._grid.get((row_idx, res_col)), self._grid.get((row_idx, gua_col))]

            data = self._extract_cols_data(cols)

            # 累積結果
            if data.original_result:
                all_results.append(data.original_result)
            if data.result:
                all_results.append(data.result)

            # 累積保底
            if data.original_guaranteed:
                all_gua.append(data.original_guaranteed)
            if data.guaranteed:
                all_gua.append(data.guaranteed)

            if data.is_duplicate:
                is_dup_flag = True

            if data.result_pos:
                result_pos = data.result_pos
            if data.guaranteed_pos:
                gua_arrows.append(data.guaranteed_pos)

        # 後處理：決定最終的 result 與 original_result
        res_text, orig_res_text, is_dup = self._finalize_results(all_results, is_dup_flag)
        gua_text, orig_gua_text = self._finalize_guaranteed(all_gua)

        return GachaSlot(
            id=Position(slot_id[0], slot_id[1]),
            result=res_text,
            guaranteed=gua_text,
            next_pos=gua_arrows[-1] if gua_arrows else None,
            original_result=orig_res_text,
            original_guaranteed=orig_gua_text,
            is_duplicate_rare=is_dup,
            single_next_pos=result_pos,
            original_next_pos=gua_arrows[0] if len(gua_arrows) > 1 else None,
        )

    def _build_cell_grid(self, rows: List) -> Tuple[Dict[Tuple[int, int], Tag], Dict[int, int]]:
        """還原表格的 (列, 欄) 網格，處理 rowspan / colspan 造成的欄位位移。

        Returns:
            (grid, cell_cols)：grid 以儲存格起始的 (列, 欄) 為鍵；
            cell_cols 以 id(儲存格) 查詢它所在的欄。
        """
        occupied: Set[Tuple[int, int]] = set()
        grid: Dict[Tuple[int, int], Tag] = {}
        cell_cols: Dict[int, int] = {}
        for row_idx, row in enumerate(rows):
            col = 0
            for cell in row.find_all(["td", "th"]):
                while (row_idx, col) in occupied:
                    col += 1
                rowspan = int(cell.get("rowspan", 1))
                colspan = int(cell.get("colspan", 1))
                for dr in range(rowspan):
                    for dc in range(colspan):
                        occupied.add((row_idx + dr, col + dc))
                grid[(row_idx, col)] = cell
                cell_cols[id(cell)] = col
                col += colspan
        return grid, cell_cols

    def _finalize_results(self, all_results: List[str], is_dup_flag: bool) -> Tuple[str, Optional[str], bool]:
        """後處理結果列表，決定最終 result 與 original_result。"""
        res_text = ""
        orig_res_text = None
        is_dup = is_dup_flag
        
        if all_results:
            if len(all_results) > 1:
                res_text = all_results[-1]
                orig_res_text = all_results[0]
                is_dup = True
            else:
                res_text = all_results[0]
        
        return res_text, orig_res_text, is_dup

    def _finalize_guaranteed(self, all_gua: List[str]) -> Tuple[str, Optional[str]]:
        """後處理保底列表，決定最終 guaranteed 與 original_guaranteed。"""
        gua_text = ""
        orig_gua_text = None
        
        if all_gua:
            gua_text = all_gua[-1]
            if len(all_gua) > 1:
                orig_gua_text = all_gua[0]
        
        return gua_text, orig_gua_text



    def _extract_cols_data(self, target_cols: Sequence[Optional[Tag]]) -> _ColsData:
        """從「結果欄、必中欄」中提取名稱與箭頭。

        結果欄與必中欄的箭頭意義不同：結果欄的箭頭是重複稀有時單抽的落點，
        必中欄的箭頭是必中十一連的落點。兩者必須分開回傳，不能互相覆蓋。

        Args:
            target_cols: [結果欄, 必中欄]；欄位不存在（被 rowspan 蓋住）時以 None 表示。
        """
        res_text = ""
        gua_text = ""
        result_pos: Optional[Position] = None
        gua_pos: Optional[Position] = None
        orig_res = None
        is_dup = False
        orig_gua = None

        result_col = target_cols[0] if len(target_cols) > 0 else None
        gua_col = target_cols[1] if len(target_cols) > 1 else None

        if result_col is not None:
            # If multiple names (Duplicate Rare), usually [Original, Replacement]. Pick last.
            valid_names = self._filter_cat_names(result_col.find_all("a"))

            if valid_names:
                res_text = valid_names[-1]
                if len(valid_names) > 1:
                    orig_res = valid_names[0]
                    is_dup = True

            result_pos = self._find_arrow(result_col)

        if gua_col is not None:
            # For Guaranteed, usually single name.
            gua_names = self._filter_cat_names(gua_col.find_all("a"))

            if gua_names:
                gua_text = gua_names[-1]
                if len(gua_names) > 1:
                    orig_gua = gua_names[0]

            gua_pos = self._find_arrow(gua_col)

        return _ColsData(res_text, gua_text, result_pos, gua_pos, orig_res, is_dup, orig_gua)

    def _find_arrow(self, cell: Tag) -> Optional[Position]:
        """讀取儲存格內的位置箭頭（如 '-> 21B'、'<- 12A'）。"""
        match = re.search(self.regex_next_pos, cell.get_text(strip=True))
        if not match:
            return None
        return Position(id=int(match.group(1)), track=match.group(2))

    def _filter_cat_names(self, a_tags: List[Tag]) -> List[str]:
        """從 <a> 標籤列表中識別所有有效貓咪名稱。"""
        found_names = []
        for a in a_tags:
            text = a.get_text(strip=True)
            # Skip position links (e.g., "1A", "22B")
            if re.match(r'^\d+[AB]$', text):
                continue
            # Skip icon links (contains <img> or specific icon characters like 🐾)
            if a.find("img") or text == "🐾":
                continue
            # If it has text and isn't a position/icon, it's a potential cat name
            if text:
                found_names.append(text)
        return found_names

    def _validate_track(self, track: Track) -> bool:
        """驗證軌道是否包含連續的 ID。"""
        if not track:
            return False
        
        expected_id = track[0].id.id
        for slot in track:
            if slot.id.id != expected_id:
                return False
            expected_id += 1
        return True

    def get_all_cats(self) -> Set[str]:
        """獲取所有軌道中出現過的貓咪種類集合。"""
        cats = set()
        for track in [self.track_a, self.track_b]:
            for slot in track:
                if isinstance(slot.result, str) and slot.result:
                    cats.add(slot.result)
                if isinstance(slot.guaranteed, str) and slot.guaranteed:
                    cats.add(slot.guaranteed)
        return cats

    def get_tracks(self) -> Tuple[Track, Track]:
        return self.track_a, self.track_b
