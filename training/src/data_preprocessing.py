import os
import h5py
import numpy as np
import random

# Directory paths
DATA_DIR = r'C:\Users\72161\Downloads\dataset1'
# DATA_DIR = r'C:\kai\Radar_Gesture_HandOff\data\0702_test'
PROCESSED_DATA_DIR = os.path.join('data', 'processed_data')
TRAIN_DATA_FILE = os.path.join(PROCESSED_DATA_DIR, 'training_dataset.npz')
VAL_DATA_FILE = os.path.join(PROCESSED_DATA_DIR, 'validation_dataset.npz')
TEST_DATA_FILE = os.path.join(PROCESSED_DATA_DIR, 'test_dataset.npz')
ALL_DATA_FILE = os.path.join(PROCESSED_DATA_DIR, 'all_dataset.npz')

# Split
train_split = 0.7
val_split = 0.15
test_split = 0.15

gesture_to_label = {
    'background': 0, 
    'left2right': 1,
    'right2left': 2,
    'up2down': 3,
    'down2up': 4,
    'push': 5,
    'circle': 6
}

all_dirs = [d for d in os.listdir(DATA_DIR) if os.path.isdir(os.path.join(DATA_DIR, d))]
gesture_types = [g for g in gesture_to_label.keys() if g in all_dirs]

print(f"Gesture mapping: {gesture_to_label}")
print(f"Gesture types order for processing: {gesture_types}")


def load_h5_file(file_path):
    """
    Load a .h5 file and extract 'DS1' and 'LABEL' arrays.
    """
    with h5py.File(file_path, 'r') as f:
        ds1 = np.array(f['DS1'], dtype=np.float32)
        label = np.array(f['LABEL']).astype(np.int32)
    return ds1, label


def generate_ground_truth(label, gesture_label, total_classes, max_length, background_label):
    """
    Generate a ground truth array based on the label.
    """
    ground_truth = np.zeros((max_length, total_classes))
    gesture_indices = np.where(label == 1)[0]

    if len(gesture_indices) == 0:
        ground_truth[:, background_label] = 1  # Background only
        return ground_truth

    for segment in np.split(gesture_indices, np.where(np.diff(gesture_indices) > 1)[0] + 1):
        if segment.size == 0:
            continue
        start_idx, end_idx = segment[0], segment[-1]
        length = end_idx - start_idx + 1
        center = length // 2
        x = np.arange(length) - center
        sigma = length / 6
        gaussian_curve = np.exp(-0.5 * (x / sigma) ** 2)
        gaussian_curve /= gaussian_curve.max()
        ground_truth[start_idx:end_idx + 1, gesture_label] = gaussian_curve

    other_labels = [l for l in range(total_classes) if l != background_label]
    ground_truth[:, background_label] = 1 - ground_truth[:, other_labels].sum(axis=1)
    return ground_truth


def _process_and_save(feature_list, max_length, total_classes, output_path, background_label):
    """
    Helper function to process a list of features, pad them, generate ground truths, and save to a .npz file.
    """
    if not feature_list:
        print(f"Warning: No data to process for {output_path}. Skipping.")
        return

    features_padded = []
    labels_padded = []
    ground_truths_padded = []

    for ds1, gesture_label, label in feature_list:
        
        # 1. RDI 正規化 (Channel 0)
        # 用 Log 壓縮數值範圍，避免大數值 (如 10000) 蓋過 Phase
        # 加上 1 是為了避免 log(0) 錯誤
        ds1[0] = np.log10(ds1[0] + 1)
        
        # 2. Phase 正規化 (Channel 1) - 最關鍵的一步！
        # 將相位從 -PI~PI 壓縮到 -1~1
        ds1[1] = ds1[1] / np.pi

        
        pad_width = ((0, 0), (0, 0), (0, 0), (0, max_length - ds1.shape[-1]))
        ds1_padded = np.pad(ds1, pad_width, mode='constant', constant_values=0)
        padded_label = np.full((max_length,), 0)
        padded_label[:len(label)] = label.squeeze()
        ground_truth = generate_ground_truth(padded_label, gesture_label, total_classes, max_length, background_label)

        features_padded.append(ds1_padded)
        labels_padded.append(padded_label)
        ground_truths_padded.append(ground_truth)

    features_padded = np.array(features_padded, dtype=np.float32)
    labels_padded = np.array(labels_padded, dtype=np.int32)
    ground_truths_padded = np.array(ground_truths_padded, dtype=np.float32)

    np.savez(
        output_path,
        features=features_padded,
        labels=labels_padded,
        ground_truths=ground_truths_padded,
    )
    print(f"Saved processed data to {output_path} ({len(feature_list)} samples)")


def process_data():
    """
    Process all .h5 files from each gesture folder and save the processed data.
    """
    if not os.path.exists(PROCESSED_DATA_DIR):
        os.makedirs(PROCESSED_DATA_DIR)

    all_features = []
    max_length = 0

    # Load files and determine maximum frame length
    for gesture in gesture_types:
        gesture_label = gesture_to_label[gesture]
        gesture_dir = os.path.join(DATA_DIR, gesture)
        for file_name in os.listdir(gesture_dir):
            if file_name.endswith('.h5'):
                file_path = os.path.join(gesture_dir, file_name)
                ds1, label = load_h5_file(file_path)
                max_length = max(max_length, ds1.shape[-1])
                all_features.append((ds1, gesture_label, label))

    total_classes = max(gesture_to_label.values()) + 1 if gesture_to_label else 0
    background_label = gesture_to_label.get('background', 0)

    # Shuffle and split the data
    random.shuffle(all_features)
    train_end = int(len(all_features) * train_split)
    val_end = train_end + int(len(all_features) * val_split)

    train_features = all_features[:train_end]
    val_features = all_features[train_end:val_end]
    test_features = all_features[val_end:]

    print(f"\nTotal samples: {len(all_features)}")
    print(f"Training samples: {len(train_features)}")
    print(f"Validation samples: {len(val_features)}")
    print(f"Test samples: {len(test_features)}")
    print(f"Max frame length: {max_length}\n")

    # Process and save training data
    _process_and_save(train_features, max_length, total_classes, TRAIN_DATA_FILE, background_label)
    # Process and save validation data
    _process_and_save(val_features, max_length, total_classes, VAL_DATA_FILE, background_label)
    # Process and save test data
    _process_and_save(test_features, max_length, total_classes, TEST_DATA_FILE, background_label)

    _process_and_save(all_features, max_length, total_classes, ALL_DATA_FILE, background_label)



if __name__ == '__main__':
    process_data()
