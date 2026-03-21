import sys
from PySide2.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QLabel, QProgressBar, QHBoxLayout, QSpacerItem, QSizePolicy
)
from PySide2.QtCore import Qt, QTimer
from PySide2.QtGui import QColor, QPalette


class GestureGUI(QWidget):
    """
    PySide2 手勢辨識 GUI，顯示 2 種手勢的機率條狀圖，並突顯當前辨識結果。
    """

    def __init__(self):
        super().__init__()
        self.setWindowTitle("Gesture Recognition")
        self.resize(600, 400)

        # **主要 Layout**
        main_layout = QVBoxLayout()

        # **當前手勢標籤**
        self.current_gesture_label = QLabel("Current gesture: Background")
        self.current_gesture_label.setAlignment(Qt.AlignCenter)
        self.current_gesture_label.setStyleSheet(
            "font-size: 20px; font-weight: bold; padding: 10px; background-color: lightgray; border-radius: 5px;"
        )
        main_layout.addWidget(self.current_gesture_label)

        # **進度條區域**
        self.hbox = QHBoxLayout()

        # **手勢名稱 & 條狀圖（改成兩個新手勢 + 背景）**
        self.gesture_names = ['background', 'left2right', 'right2left', 'up2down', 'down2up', 'push', 'circle']
        self.bars = {}  # 存放進度條物件

        # **可調整的參數**
        self.BAR_WIDTH = 15  # 進度條寬度
        self.bar_colors = {
            "background": "#808080",
            "left2right": "#FFA500",
            "right2left": "#1E90FF",
            "up2down": "#9400D3",
            "down2up": "#DC143C",
            "push": "#00CED1",
            "circle": "#3CB371"
        }
        self.gesture_colors = {
            "background": "lightgray", 
            "left2right": "#FFD700", 
            "right2left": "#ADD8E6", 
            "up2down": "#E6E6FA", 
            "down2up": "#FFCCCB",
            "push": "#AFEEEE",
            "circle": "#90EE90"
        }

        # **Spacer 讓條狀圖置中**
        self.hbox.addSpacerItem(QSpacerItem(40, 20, QSizePolicy.Expanding, QSizePolicy.Minimum))

        for name in self.gesture_names:
            # 建立一個垂直 Layout
            v_layout = QVBoxLayout()

            # 進度條
            bar = QProgressBar()
            bar.setOrientation(Qt.Vertical)
            bar.setRange(0, 100)
            bar.setValue(0)
            bar.setTextVisible(False)  # 不顯示文字
            bar.setStyleSheet(f"QProgressBar::chunk {{ background-color: {self.bar_colors[name]}; }}")
            bar.setFixedWidth(self.BAR_WIDTH)  # 設定進度條寬度
            v_layout.addWidget(bar, alignment=Qt.AlignBottom)

            # 手勢標籤
            label = QLabel(name)
            label.setAlignment(Qt.AlignCenter)
            v_layout.addWidget(label, alignment=Qt.AlignCenter)

            self.hbox.addLayout(v_layout)

            # 在每個進度條之間加入 SpacerItem，讓它們等距排列
            self.hbox.addSpacerItem(QSpacerItem(20, 20, QSizePolicy.Expanding, QSizePolicy.Minimum))

            self.bars[name] = bar  # 存入字典

        # **Spacer 讓條狀圖置中**
        self.hbox.addSpacerItem(QSpacerItem(40, 20, QSizePolicy.Expanding, QSizePolicy.Minimum))

        main_layout.addLayout(self.hbox)
        self.setLayout(main_layout)

    def update_probabilities(self, probabilities_dict):
        """
        更新所有進度條，並找出機率最高的結果。
        probabilities_dict: {gesture_name: probability_value, ...}
        """
        max_prob = -1.0
        current_gesture = "Background"
        
        # 遍歷字典來更新所有進度條
        for name, prob in probabilities_dict.items():
            if name in self.bars:
                self.bars[name].setValue(int(prob * 100))
                
                # 找出機率最高的結果
                if prob > max_prob:
                    max_prob = prob
                    current_gesture = name

        # 更新中央標籤
        self.current_gesture_label.setText(f"Current gesture: {current_gesture}")
        self.current_gesture_label.setStyleSheet(
            f"font-size: 20px; font-weight: bold; padding: 10px; background-color: {self.gesture_colors[current_gesture]}; border-radius: 5px;"
        )

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = GestureGUI()
    window.show()

    # 測試數據：每秒更新一次
    import random

    def simulate_data():
        num_gestures = len(window.gesture_names)
        
        # 1. 產生 N 個隨機數
        probs = [random.uniform(0.0, 1.0) for _ in range(num_gestures)]
        
        # 2. 將它們標準化，使其總和為 1
        total = sum(probs)
        if total == 0:
            # 避免除以零
            probs = [1.0 / num_gestures] * num_gestures
        else:
            probs = [p / total for p in probs]

        # 3. 將列表轉換為 {手勢名稱: 機率} 的字典
        probs_dict = dict(zip(window.gesture_names, probs))

        # 4. 傳遞字典給更新函數
        window.update_probabilities(probs_dict)


    timer = QTimer()
    timer.timeout.connect(simulate_data)
    timer.start(500)  # 每 500 毫秒 (0.5 秒) 更新一次

    sys.exit(app.exec_())
