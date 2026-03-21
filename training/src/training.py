import os
import time
import numpy as np
import matplotlib.pyplot as plt

import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader
from tqdm import tqdm  # Progress bar
from sklearn.metrics import confusion_matrix, classification_report

# -------------------------------
# Hyperparameters and Paths
# -------------------------------
TRAINING_DATA_DIR = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\data\processed_data\training_dataset.npz"
VAL_DATA_DIR = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\data\processed_data\validation_dataset.npz"
WINDOW_SIZE = 30 #more
STEP_SIZE = 1
BATCH_SIZE = 32
EPOCHS = 50  #30
LEARNING_RATE = 1e-4
NUM_CLASSES = 7

# Create a timestamped folder for saving models
timestamp = time.strftime('%Y%m%d_%H%M%S', time.localtime(time.time()))
MODEL_SAVE_PATH = os.path.join("output", "models", timestamp)
os.makedirs(MODEL_SAVE_PATH, exist_ok=True)

# Define gesture types (order must match training)
gesture_types = ['background', 'left2right', 'right2left', 'up2down', 'down2up', 'push', 'circle']

# -------------------------------
# Custom Dataset for memory-efficient loading
# -------------------------------
class GestureDataset(Dataset):
    """
    Custom PyTorch Dataset that loads data and creates windows on the fly.
    """
    def __init__(self, data_path, window_size, step_size):
        print(f"Initializing dataset from: {data_path}")
        data = np.load(data_path)
        
        # Reshape features to be (num_clips, 2, 32, 32, frames)
        # And ground_truths to (num_clips, frames, num_classes)
        self.features = data['features']
        self.ground_truths = data['ground_truths']
        self.window_size = window_size
        self.step_size = step_size

        self.num_clips = self.features.shape[0]
        self.frames_per_clip = self.features.shape[-1]
        
        # Calculate the total number of samples
        self.num_samples_per_clip = (self.frames_per_clip - self.window_size) // self.step_size + 1
        self.total_samples = self.num_clips * self.num_samples_per_clip

        print(f"Found {self.num_clips} clips, {self.frames_per_clip} frames per clip.")
        print(f"Total samples: {self.total_samples}")

    def __len__(self):
        return self.total_samples

    def __getitem__(self, idx):
        clip_idx = idx // self.num_samples_per_clip
        start_in_clip = (idx % self.num_samples_per_clip) * self.step_size
        
        start = start_in_clip
        end = start + self.window_size
        mid = start + self.window_size // 2

        # Extract window and label
        window_feature = self.features[clip_idx, ..., start:end]  # Shape: (2, 32, 32, WINDOW_SIZE)
        label_soft = self.ground_truths[clip_idx, mid, :]         # Shape: (num_classes,)

        # Transpose to (2, WINDOW_SIZE, 32, 32) for the model
        window_feature = np.transpose(window_feature, (0, 3, 1, 2))

        # Create contiguous arrays
        window_feature = np.ascontiguousarray(window_feature)
        label_soft = np.ascontiguousarray(label_soft)

        return torch.from_numpy(window_feature).float(), torch.from_numpy(label_soft).float()

# -------------------------------
# 3D CNN Model Definition
# -------------------------------
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

# -------------------------------
# Model Training Function
# -------------------------------
def train_model(train_loader, val_loader, train_dataset_size, val_dataset_size, device):
    """
    Train the 3D CNN model using the provided training and validation data.
    
    Args:
        train_loader (DataLoader): DataLoader for the training set.
        val_loader (DataLoader): DataLoader for the validation set.
        device (torch.device): The device to train the model on (CPU or CUDA).
        train_dataset_size (int): Total number of samples in the training set.
        val_dataset_size (int): Total number of samples in the validation set.
        
    Returns:
        model: The trained model.
        history: A dictionary with training/validation losses and accuracies.
    """
    class_weights = torch.tensor([
        0.1,  # Background: 表現極好，降到最低，讓模型不要浪費力氣在這裡。
        2.5,  # Left2Right: 表現尚可，稍微加強即可。
        2.5,  # Right2Left: (原 2.5) 仍有 18 個被吸走，加大力度。
        0.4,  # Up2Down: (原 0.3) 維持低檔。注意：不能太低(如0.01)，否則模型會完全放棄預測它，導致 Recall 暴跌。
        3.5,  # Down2Up: (原 1.2 -> 重點修正) 它有 24 個樣本被誤判為 Up2Down，這是重災區，必須給予極高權重！
        3.5,  # Push: (原 1.0 -> 重點修正) 這是最慘的類別之一 (Recall ~50%)，必須大幅加權逼模型認出它。
        3.0   # Circle: (原 2.0 -> 加強) 仍有 22 個樣本被吸走，需要再加強。
    ]).to(device)
    model = Gesture2DCNN_LSTM(num_classes=NUM_CLASSES).to(device)
    optimizer = optim.Adam(model.parameters(), lr=1e-4, weight_decay=1e-5)
    criterion = nn.CrossEntropyLoss(weight=class_weights)
    
    train_losses, val_losses = [], []
    train_accuracies, val_accuracies = [], []

    best_val_acc = 0.0
    # ---- Early Stopping ----
    patience = 50
    no_improve_count = 0
    best_val_loss = float('inf')
    # ------------------------

    for epoch in range(EPOCHS):
        model.train()
        running_loss = 0.0
        epoch_train_correct = 0
        epoch_train_total = 0

        # Training loop
        train_pbar = tqdm(train_loader, desc=f"Epoch [{epoch+1}/{EPOCHS}] (Training)", unit="batch")
        for batch_x, batch_y in train_pbar:
            batch_x, batch_y = batch_x.to(device), batch_y.to(device)
            optimizer.zero_grad()
            outputs = model(batch_x)
            target_indices = torch.argmax(batch_y, dim=1)
            loss = criterion(outputs, target_indices)
            loss.backward()
            optimizer.step()

            running_loss += loss.item() * batch_x.size(0)

            preds = torch.argmax(outputs, dim=1)
            targets = torch.argmax(batch_y, dim=1)
            epoch_train_correct += (preds == targets).sum().item()
            epoch_train_total += batch_x.size(0)

        epoch_loss = running_loss / train_dataset_size
        train_losses.append(epoch_loss)
        train_acc = epoch_train_correct / epoch_train_total
        train_accuracies.append(train_acc)

        # Validation
        model.eval()
        val_running_loss = 0.0
        epoch_val_correct = 0
        epoch_val_total = 0
        all_preds = []   # 新增：收集所有預測
        all_targets = [] # 新增：收集所有真實標籤

        with torch.no_grad():
            for val_x, val_y in val_loader:
                val_x, val_y = val_x.to(device), val_y.to(device)
                val_outputs = model(val_x)
                val_loss = criterion(val_outputs, torch.argmax(val_y, dim=1))
                val_running_loss += val_loss.item() * val_x.size(0)

                val_preds = torch.argmax(val_outputs, dim=1)
                val_targets = torch.argmax(val_y, dim=1)
                epoch_val_correct += (val_preds == val_targets).sum().item()
                epoch_val_total += val_x.size(0)
                all_preds.extend(val_preds.cpu().numpy())
                all_targets.extend(val_targets.cpu().numpy())

        # 計算 Metrics
        val_epoch_loss = val_running_loss / val_dataset_size
        val_losses.append(val_epoch_loss)
        
        # 計算 Accuracy
        val_acc = np.mean(np.array(all_preds) == np.array(all_targets))
        val_accuracies.append(val_acc)

        print(f"[Epoch {epoch+1}/{EPOCHS}] Train Loss: {epoch_loss:.4f} || Val Loss: {val_epoch_loss:.4f} || "
              f"Train Acc: {train_acc:.4f} || Val Acc: {val_acc:.4f}")
        
        # --- 新增：每 5 個 Epoch 打印一次 Confusion Matrix ---
        if (epoch + 1) % 5 == 0 or (epoch + 1) == EPOCHS:
            print("\n" + "="*30)
            print(f"Epoch {epoch+1} Confusion Matrix:")
            cm = confusion_matrix(all_targets, all_preds, labels=range(NUM_CLASSES))
            print(cm)
            
            # 簡單檢查 Up2Down (類別索引 3) 的情況
            up2down_pred_count = np.sum(np.array(all_preds) == 3)
            print(f"本輪預測為 Up2Down 的總次數: {up2down_pred_count} (若過高請警惕)")
            print("="*30 + "\n")

        # # ---- Save best model & Early Stopping: use loss function ----
        # if val_epoch_loss < best_val_loss:
        #     best_val_loss = val_epoch_loss
        #     no_improve_count = 0
        #     best_model_path = os.path.join(MODEL_SAVE_PATH, f"epoch_{epoch+1}_valLoss_{val_epoch_loss:.4f}.pth")
        #     torch.save(model.state_dict(), best_model_path)
        #     print(f"New best model saved: {best_model_path}")
        # else:
        #     no_improve_count += 1
        #     print(f"No improvement for {no_improve_count} epochs.")

        # if no_improve_count >= patience:
        #     print(f"Early stopping triggered at epoch {epoch+1}!")
        #     break
        # # ------------------------------------------

        # ---- Save best model & Early Stopping: use accuracy function ----
        if val_acc > best_val_acc:
            best_val_acc = val_acc
            no_improve_count = 0
            best_model_path = os.path.join(MODEL_SAVE_PATH, f"epoch_{epoch+1}_valAcc_{val_acc:.4f}.pth")
            torch.save(model.state_dict(), best_model_path)
            print(f"New best model saved: {best_model_path}")
        else:
            no_improve_count += 1
            print(f"No improvement for {no_improve_count} epochs.")
        
        if no_improve_count >= patience:
            print(f"Early stopping triggered at epoch {epoch+1}!")
            break
        # ----------------------------------------

    return model, {
        'train_loss': train_losses,
        'val_loss': val_losses,
        'train_acc': train_accuracies,
        'val_acc': val_accuracies
    }

# -------------------------------
# Plot Training History
# -------------------------------
def plot_history(history):
    """
    Plot training and validation loss and accuracy curves.
    """
    epochs_range = range(1, len(history['train_loss']) + 1)
    plt.figure(figsize=(12, 5))
    
    # Loss curves
    plt.subplot(1, 2, 1)
    plt.plot(epochs_range, history['train_loss'], label='Training Loss')
    plt.plot(epochs_range, history['val_loss'], label='Validation Loss')
    plt.title('Loss Curves')
    plt.xlabel('Epochs')
    plt.ylabel('Loss')
    plt.legend()
    
    # Accuracy curves
    plt.subplot(1, 2, 2)
    plt.plot(epochs_range, history['train_acc'], label='Training Accuracy')
    plt.plot(epochs_range, history['val_acc'], label='Validation Accuracy')
    plt.title('Accuracy Curves')
    plt.xlabel('Epochs')
    plt.ylabel('Accuracy')
    plt.legend()
    
    plt.tight_layout()
    plt.show()

# -------------------------------
# Main Function
# -------------------------------
def main():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Using device: {device}")

    # --- 加入這段檢查代碼 ---
    print("正在檢查數據集形狀...")
    temp_data = np.load(TRAINING_DATA_DIR)
    gt_shape = temp_data['ground_truths'].shape
    print(f"Ground Truths Shape: {gt_shape}")
    
    actual_num_classes = gt_shape[-1]
    print(f"數據集包含的類別數: {actual_num_classes}")
    print(f"程式碼設定的類別數 (NUM_CLASSES): {NUM_CLASSES}")
    
    if actual_num_classes != NUM_CLASSES:
        print(f"⚠️ 警告: 類別數量不匹配！請將 NUM_CLASSES 改為 {actual_num_classes}")
        return  # 停止執行，先去改程式碼
    
    # Create Datasets
    train_dataset = GestureDataset(TRAINING_DATA_DIR, WINDOW_SIZE, STEP_SIZE)
    val_dataset = GestureDataset(VAL_DATA_DIR, WINDOW_SIZE, step_size=10)

    # Create DataLoaders
    train_loader = DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True, num_workers=0, pin_memory=True)
    val_loader = DataLoader(val_dataset, batch_size=BATCH_SIZE, shuffle=False, num_workers=0, pin_memory=True)

    # Train the model
    model, history = train_model(train_loader, val_loader, len(train_dataset), len(val_dataset), device)
    
    plot_history(history)

if __name__ == "__main__":
    main()
