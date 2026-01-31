import re
from bs4 import BeautifulSoup, Tag
from typing import List, Optional, Union
from .models import GachaSlot, Position, ParseStatus, Track

class BCParser:
    """將 BC Godfat 的 HTML 內容解析為結構化資料。"""
    
    def __init__(self, html_content: str):
        self.html_content = html_content
        self.regex_a = r'^N([0-9]+)(A)$'
        self.regex_b = r'^N([0-9]+)(B)$'
        self.regex_next_pos = r'(?:->|<-)\s*(\d+)([AB])'
        self.track_a, self.track_b = self._parse_gacha_table()

    def _parse_gacha_table(self) -> tuple[Track, Track] :
        """解析轉蛋表格並回傳 Track A 與 Track B 的 tuple。"""
        soup = BeautifulSoup(self.html_content, "html.parser")
        table = soup.find("table")
        if not table:
            raise ValueError("無法解析 HTML: 找不到轉蛋表格")

        rows = table.find_all("tr")
        
        track_a = self._parse_track_a(rows)
        track_b = self._parse_track_b(rows)

        # Validate tracks
        if not self._validate_track(track_a) or not self._validate_track(track_b):
            # If validation fails, we can return empty tracks or 
            # perhaps the partially parsed tracks depending on requirements.
            # For now, let's keep it simple as per the plan.
            raise ValueError("Invalid track")
        
        return (track_a, track_b)

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
        match = re.match(self.regex_a, id_td.get("id", ""))
        slot_id = (int(match.group(1)), match.group(2))
        rowspan = int(id_td.get("rowspan", 1))
        
        res_text = ""
        gua_text = ""
        next_pos = None

        for i in range(rowspan):
            curr_row = all_rows[start_idx + i]
            curr_tds = curr_row.find_all("td")
            
            # Track A: Data is at the beginning
            start_offset = 1 if i == 0 else 0
            target_cols = curr_tds[start_offset : start_offset + 2]

            res, gua, pos = self._extract_cols_data(target_cols)
            
            # Update result and guaranteed (allow overwrite for Duplicate Rare corrections)
            if res:
                res_text = res
            if gua:
                gua_text = gua
            if pos:
                next_pos = pos

        return GachaSlot(
            id=slot_id,
            result=res_text,
            guaranteed=gua_text,
            next_pos=next_pos
        )

    def _parse_slot_b(self, all_rows: List, start_idx: int, id_td: Tag) -> GachaSlot:
        """解析 Track B 單一格子的資料。"""
        match = re.match(self.regex_b, id_td.get("id", ""))
        slot_id = (int(match.group(1)), match.group(2))
        rowspan = int(id_td.get("rowspan", 1))
        
        res_text = ""
        gua_text = ""
        next_pos = None

        for i in range(rowspan):
            curr_row = all_rows[start_idx + i]
            curr_tds = curr_row.find_all("td")
            
            # Track B: Data is at the end
            end_offset = 1 if i == 0 else 0
            target_cols = curr_tds[-(end_offset + 2) : len(curr_tds) - end_offset]

            res, gua, pos = self._extract_cols_data(target_cols)
            
            # Update result and guaranteed (allow overwrite for Duplicate Rare corrections)
            if res:
                res_text = res
            if gua:
                gua_text = gua
            if pos:
                next_pos = pos

        return GachaSlot(
            id=slot_id,
            result=res_text,
            guaranteed=gua_text,
            next_pos=next_pos
        )

    def _extract_cols_data(self, target_cols: List[Tag]) -> tuple[str, str, Optional[Position]]:
        """從欄位中提取結果、保底與下一個位置的通用邏輯。

        Args:
            target_cols (List[Tag]): 包含轉蛋結果資料的 <td> 元素。

        Returns:
            tuple[str, str, Optional[Position]]: (result_text, guaranteed_text, next_position)
        """
        res_text = ""
        gua_text = ""
        next_pos: Optional[Position] = None

        if len(target_cols) > 0:
            # Find cat name in the first data column (Result)
            a_tags = target_cols[0].find_all("a")
            # If multiple names (Duplicate Rare), usually [Original, Replacement]. Pick last.
            valid_names = self._filter_cat_names(a_tags)
            res_text = valid_names[-1] if valid_names else ""
            
            # Check for track switch (Duplicate Rare) in Result column
            full_text_0 = target_cols[0].get_text(strip=True)
            pos_match_0 = re.search(self.regex_next_pos, full_text_0)
            if pos_match_0:
                next_pos = Position(id=int(pos_match_0.group(1)), track=pos_match_0.group(2))

        if len(target_cols) > 1:
            # Find cat name in the guaranteed column
            a_tags = target_cols[1].find_all("a")
            # For Guaranteed, usually single name.
            gua_names = self._filter_cat_names(a_tags)
            gua_text = gua_names[0] if gua_names else ""
            
            # Extract next position from guaranteed column (overwrite if found, usually higher priority or distinct)
            full_text = target_cols[1].get_text(strip=True)
            pos_match = re.search(self.regex_next_pos, full_text)
            if pos_match:
                next_pos = Position(id=int(pos_match.group(1)), track=pos_match.group(2))
        
        return res_text, gua_text, next_pos

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
        
        expected_id = track[0].id[0]
        for slot in track:
            if slot.id[0] != expected_id:
                return False
            expected_id += 1
        return True

    def get_all_cats(self) -> set[str]:
        """獲取所有軌道中出現過的貓咪種類集合。"""
        cats = set()
        for track in [self.track_a, self.track_b]:
            for slot in track:
                if isinstance(slot.result, str) and slot.result:
                    cats.add(slot.result)
                if isinstance(slot.guaranteed, str) and slot.guaranteed:
                    cats.add(slot.guaranteed)
        return cats

    def get_tracks(self) -> tuple[Track, Track]:
        return self.track_a, self.track_b
