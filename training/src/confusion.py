import os
import numpy as np
import torch
import torch.nn as nn
import matplotlib.pyplot as plt
import seaborn as sns
from sklearn.metrics import confusion_matrix, classification_report

# ---------------------------------------------------
# Parameters
# ---------------------------------------------------
# TEST_DATA_FILE = r"C:\kai\Radar_Gesture_HandOff\data\processed_data\0702_val_10_test.npz"
TEST_DATA_FILE = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\data\processed_data\test_dataset.npz"
MODEL_PATH     = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\output\up2downnew.pth"

WINDOW_SIZE    = 30
HIGH_TH        = 0.5
LOW_TH         = 0.1

# 7 個手勢的名稱 (順序與模型輸出一致)
BASE_GESTURES = ['background', 'left2right', 'right2left', 'up2down', 'down2up', 'push', 'circle']
NUM_BASE_CLASSES = len(BASE_GESTURES) # 7
NUM_CLASSES = len(BASE_GESTURES)

# Clip-level classes: 0=Background, 1..6=Gestures, 7=Multi-Gesture, 8=Incomplete
row_names      = BASE_GESTURES # True labels for confusion matrix (只包含基礎類別)
col_names      = BASE_GESTURES + ['Multi-Gesture', 'Incomplete'] # Predicted labels (包含特殊情況)
true_labels    = list(range(NUM_BASE_CLASSES)) # [0, 1, ..., 6]
pred_labels    = list(range(NUM_BASE_CLASSES + 2)) # [0, 1, ..., 8] all possible predicted labels

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
print(f"Using device: {device}")

# ---------------------------------------------------
# 1. Model definition
# ---------------------------------------------------
class Gesture2DCNN_LSTM(nn.Module):
    def __init__(self, num_classes = NUM_CLASSES):
        super(Gesture2DCNN_LSTM, self).__init__()
        self.features = nn.Sequential(
            nn.Conv2d(in_channels=2, out_channels=8, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),
            
            nn.Conv2d(8, 16, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),
            
            nn.Conv2d(16, 32, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),

            nn.Flatten(),
            nn.Linear(32*4*4, 128),
            nn.BatchNorm1d(128),
            nn.ReLU(),
            nn.Dropout(0.5)

        )
        self.lstm = nn.LSTM(input_size=128, hidden_size=128, num_layers=1, batch_first=True)
        self.classifier = nn.Linear(128, num_classes)
    
    def forward(self, x):
        B, C, T, H, W = x.shape
        x = x.permute(0, 2, 1, 3, 4)   # (B, T, C, H, W)
        cnn_in = x.reshape(B*T, C, H, W)        # CNN 處理每一幀

        cnn_out = self.features(cnn_in)

        lstm_in = cnn_out.reshape(B, T, -1)      # (B, T, feature_dim)

        lstm_out, _ = self.lstm(lstm_in)
        last_out = lstm_out[:, -1, :]

        out = self.classifier(last_out)
        return out


# ---------------------------------------------------
# 2. Load data & model
# ---------------------------------------------------
data = np.load(TEST_DATA_FILE, allow_pickle=True)
features      = data['features']      # object array: (N_clips,)
ground_truths = data['ground_truths']

model = Gesture2DCNN_LSTM().to(device)
model.load_state_dict(torch.load(MODEL_PATH, map_location=device, weights_only=False))
model.eval()

N_clips = len(features)
print(f"Found {N_clips} clips.")

# ---------------------------------------------------
# Helpers
# ---------------------------------------------------
def extract_window(clip_feat, center_frame, window_size=WINDOW_SIZE):
    total = clip_feat.shape[-1]; half = window_size//2
    start = max(0, min(center_frame-half, total-window_size))
    end   = start + window_size
    win   = clip_feat[..., start:end]              # (2,32,32,window)
    win   = np.transpose(win, (0,3,1,2))           # (2,window,32,32)
    return np.expand_dims(win, 0)                  # (1,2,window,32,32)

def find_gesture_events(gt_clip, eps=1e-7):
    events=[]; in_g=False
    for i in range(len(gt_clip)):
        if np.max(gt_clip[i,1:])>eps:
            if not in_g:
                in_g=True; start=i
        else:
            if in_g:
                events.append((start,i-1)); in_g=False
    if in_g: events.append((start,len(gt_clip)-1))
    return events


# ---------------------------------------------------
# 3. Clip‐level inference & labeling
# ---------------------------------------------------
true_clip_labels=[]; pred_clip_labels=[]

CONFIDENCE_TH = 0.8
with torch.no_grad():
    for idx in range(N_clips):
        clip_feat = features[idx]     # ndarray (2,32,32,frames)
        gt_clip   = ground_truths[idx]
        frames    = clip_feat.shape[-1]
        probs     = np.zeros((frames, NUM_BASE_CLASSES))

        # frame‐wise prediction
        for t in range(frames):
            win = extract_window(clip_feat, t)
            inp = torch.from_numpy(win).float().to(device)
            out = model(inp).cpu().numpy().squeeze()
            probs[t] = np.exp(out) / np.sum(np.exp(out))

        # dual‐threshold state sequence
        pred_seq = np.zeros(frames, dtype=int); current=0
        for t in range(frames):
            non_bg = probs[t,1:]; i_max=np.argmax(non_bg)+1; p_max=non_bg[i_max-1]
            if current==0:
                if p_max>=HIGH_TH and p_max>=CONFIDENCE_TH: current=i_max
            else:
                if p_max<LOW_TH: current=0
            pred_seq[t]=current

        # extract event labels
        events=[]; prev=0
        for lbl in pred_seq:
            if lbl!=0 and prev==0: events.append(lbl)
            prev=lbl

        # determine clip‐level label
        if len(events)==0:
            pred_lbl=0
        elif len(events)>1:
            pred_lbl=7   # Multi‐Gesture
        else:
            # single event—but check incomplete if never dropped to 0 at end
            if pred_seq[-1]!=0:
                pred_lbl=8  # Incomplete
            else:
                pred_lbl=events[0]
        pred_clip_labels.append(pred_lbl)

        # true label
        gt_events = find_gesture_events(gt_clip)
        if len(gt_events)==0:
            true_lbl=0
        elif len(gt_events)>1:
            true_lbl=3 # True label for Multi-Gesture
        else:
            s,e=gt_events[0]; mid=(s+e)//2
            true_lbl=int(np.argmax(gt_clip[mid,1:])+1)
        true_clip_labels.append(true_lbl)

# ---------------------------------------------------
# 4. Confusion matrix & report (4 true × 6 pred)
# ---------------------------------------------------
cm_full = confusion_matrix(true_clip_labels, pred_clip_labels, labels=pred_labels)
cm = cm_full[:NUM_BASE_CLASSES, :]  # only first 7 true‐label rows

print(f"Confusion Matrix ({NUM_BASE_CLASSES} true × {NUM_BASE_CLASSES+2} pred):")
print(cm)

report = classification_report(
    true_clip_labels, pred_clip_labels,
    labels=true_labels, target_names=row_names
)
print("\nClassification Report (no true ‘Multi’/‘Incomplete’):")
print(report)

# draw confusion matrix
plt.figure(figsize=(10, 8))
ax = sns.heatmap(
    cm, annot=True, fmt='d', cmap='Blues',
    xticklabels=col_names, yticklabels=row_names,
    annot_kws={"size": 12}
)

ax.set_xticklabels(col_names, rotation=45, ha='right', fontsize=10)
ax.set_yticklabels(row_names, rotation=0, va='center', fontsize=10)

plt.xlabel("Predicted", fontsize=12)
plt.ylabel("True", fontsize=12)
plt.title("Clip-level Confusion Matrix", fontsize=16)
plt.tight_layout()
plt.show()
