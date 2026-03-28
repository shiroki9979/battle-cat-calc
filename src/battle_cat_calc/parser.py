import re
from bs4 import BeautifulSoup, Tag
from typing import List, Optional, Set, Tuple, Union
from .models import GachaSlot, Position, ParseStatus, Track

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
        
        all_results = []
        all_gua = []
        is_dup_flag = False
        next_pos = None

        for i in range(rowspan):
            curr_row = all_rows[start_idx + i]
            curr_tds = curr_row.find_all("td")
            
            # 根據軌道類型決定欄位位置
            target_cols = self._get_target_cols(curr_tds, track, is_first_row=(i == 0))

            res, gua, pos, orig, dup, orig_gua = self._extract_cols_data(target_cols)
            
            # 累積結果
            if orig:
                all_results.append(orig)
            if res:
                all_results.append(res)
            
            # 累積保底
            if orig_gua:
                all_gua.append(orig_gua)
            if gua:
                all_gua.append(gua)
            
            if dup:
                is_dup_flag = True

            if pos:
                next_pos = pos

        # 後處理：決定最終的 result 與 original_result
        res_text, orig_res_text, is_dup = self._finalize_results(all_results, is_dup_flag)
        gua_text, orig_gua_text = self._finalize_guaranteed(all_gua)

        return GachaSlot(
            id=Position(slot_id[0], slot_id[1]),
            result=res_text,
            guaranteed=gua_text,
            next_pos=next_pos,
            original_result=orig_res_text,
            original_guaranteed=orig_gua_text,
            is_duplicate_rare=is_dup
        )

    def _get_target_cols(self, curr_tds: List[Tag], track: str, is_first_row: bool) -> List[Tag]:
        """根據軌道類型與列位置，取得目標欄位。"""
        if track == 'A':
            start_offset = 1 if is_first_row else 0
            return curr_tds[start_offset : start_offset + 2]
        else:  # Track B
            end_offset = 1 if is_first_row else 0
            return curr_tds[-(end_offset + 2) : len(curr_tds) - end_offset] if end_offset else curr_tds[-2:]

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



    def _extract_cols_data(self, target_cols: List[Tag]) -> Tuple[str, str, Optional[Position], Optional[str], bool, Optional[str]]:
        """從欄位中提取結果、保底、下一個位置、原始結果與重複狀態。

        Args:
            target_cols (List[Tag]): 包含轉蛋結果資料的 <td> 元素。

        Returns:
            tuple: (result_text, guaranteed_text, next_position, original_result, is_duplicate, original_guaranteed)
        """
        res_text = ""
        gua_text = ""
        next_pos: Optional[Position] = None
        orig_res = None
        is_dup = False
        orig_gua = None

        if len(target_cols) > 0:
            # Find cat name in the first data column (Result)
            a_tags = target_cols[0].find_all("a")
            # If multiple names (Duplicate Rare), usually [Original, Replacement]. Pick last.
            valid_names = self._filter_cat_names(a_tags)
            
            if valid_names:
                res_text = valid_names[-1]
                if len(valid_names) > 1:
                    orig_res = valid_names[0]
                    is_dup = True
            
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
            
            if gua_names:
                gua_text = gua_names[-1]
                if len(gua_names) > 1:
                    orig_gua = gua_names[0]
            
            # Extract next position from guaranteed column (overwrite if found, usually higher priority or distinct)
            full_text = target_cols[1].get_text(strip=True)
            pos_match = re.search(self.regex_next_pos, full_text)
            if pos_match:
                next_pos = Position(id=int(pos_match.group(1)), track=pos_match.group(2))
        
        return res_text, gua_text, next_pos, orig_res, is_dup, orig_gua

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
