using System.Windows;
using System.Windows.Markup;
using KEYCE_KODER.App.Views;

namespace KEYCE_KODER.App;

public class App : Application
{
    public App()
    {
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Couleurs partagées par les styles ci-dessous
        Resources["KK.Track"] = Theme.Editor;
        Resources["KK.Thumb"] = Theme.SurfaceHover;
        Resources["KK.ThumbHover"] = Theme.Muted;
        Resources["KK.Arrow"] = Theme.Muted;

        // Le petit carré blanc à l'intersection des deux barres de défilement
        Resources[SystemColors.ControlBrushKey] = Theme.Editor;

        Resources.MergedDictionaries.Add(
            (ResourceDictionary)XamlReader.Parse(StylesXaml));
    }

    private const string StylesXaml = """
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Bouton invisible utilisé pour les zones de page de la barre de défilement -->
  <ControlTemplate x:Key="KK.EmptyButton" TargetType="RepeatButton">
    <Border Background="Transparent"/>
  </ControlTemplate>

  <!-- Poignée arrondie -->
  <ControlTemplate x:Key="KK.ThumbTemplate" TargetType="Thumb">
    <Border x:Name="Bd" CornerRadius="4" Margin="2"
            Background="{DynamicResource KK.Thumb}"/>
    <ControlTemplate.Triggers>
      <Trigger Property="IsMouseOver" Value="True">
        <Setter TargetName="Bd" Property="Background"
                Value="{DynamicResource KK.ThumbHover}"/>
      </Trigger>
      <Trigger Property="IsDragging" Value="True">
        <Setter TargetName="Bd" Property="Background"
                Value="{DynamicResource KK.ThumbHover}"/>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <ControlTemplate x:Key="KK.VerticalBar" TargetType="ScrollBar">
    <Grid Background="{DynamicResource KK.Track}">
      <Track x:Name="PART_Track" IsDirectionReversed="True">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageUpCommand"
                        Template="{StaticResource KK.EmptyButton}"/>
        </Track.DecreaseRepeatButton>
        <Track.Thumb>
          <Thumb Template="{StaticResource KK.ThumbTemplate}"/>
        </Track.Thumb>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageDownCommand"
                        Template="{StaticResource KK.EmptyButton}"/>
        </Track.IncreaseRepeatButton>
      </Track>
    </Grid>
  </ControlTemplate>

  <ControlTemplate x:Key="KK.HorizontalBar" TargetType="ScrollBar">
    <Grid Background="{DynamicResource KK.Track}">
      <Track x:Name="PART_Track">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageLeftCommand"
                        Template="{StaticResource KK.EmptyButton}"/>
        </Track.DecreaseRepeatButton>
        <Track.Thumb>
          <Thumb Template="{StaticResource KK.ThumbTemplate}"/>
        </Track.Thumb>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageRightCommand"
                        Template="{StaticResource KK.EmptyButton}"/>
        </Track.IncreaseRepeatButton>
      </Track>
    </Grid>
  </ControlTemplate>

  <!-- Style implicite : s'applique à toutes les barres de défilement -->
  <Style TargetType="ScrollBar">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Width" Value="12"/>
    <Setter Property="Height" Value="Auto"/>
    <Setter Property="Template" Value="{StaticResource KK.VerticalBar}"/>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Width" Value="Auto"/>
        <Setter Property="Height" Value="12"/>
        <Setter Property="Template" Value="{StaticResource KK.HorizontalBar}"/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- Expander sombre avec flèche › qui pivote -->
  <ControlTemplate x:Key="KK.ExpanderToggle" TargetType="ToggleButton">
    <Border Background="Transparent" Padding="{TemplateBinding Padding}">
      <StackPanel Orientation="Horizontal">
        <TextBlock x:Name="Arrow" Text="›" FontSize="16" Width="14"
                   Foreground="{DynamicResource KK.Arrow}"
                   RenderTransformOrigin="0.5,0.5"
                   VerticalAlignment="Center">
          <TextBlock.RenderTransform>
            <RotateTransform Angle="0"/>
          </TextBlock.RenderTransform>
        </TextBlock>
        <ContentPresenter VerticalAlignment="Center"/>
      </StackPanel>
    </Border>
    <ControlTemplate.Triggers>
      <Trigger Property="IsChecked" Value="True">
        <Setter TargetName="Arrow" Property="RenderTransform">
          <Setter.Value>
            <RotateTransform Angle="90"/>
          </Setter.Value>
        </Setter>
      </Trigger>
    </ControlTemplate.Triggers>
  </ControlTemplate>

  <Style TargetType="Expander">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Expander">
          <Border BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  Background="{TemplateBinding Background}">
            <DockPanel>
              <ToggleButton DockPanel.Dock="Top"
                            Content="{TemplateBinding Header}"
                            Padding="{TemplateBinding Padding}"
                            Template="{StaticResource KK.ExpanderToggle}"
                            Focusable="False" Cursor="Hand"
                            IsChecked="{Binding IsExpanded, Mode=TwoWay,
                                        RelativeSource={RelativeSource TemplatedParent}}"/>
              <ContentPresenter x:Name="Body" Visibility="Collapsed"
                                Margin="22,0,8,8"/>
            </DockPanel>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsExpanded" Value="True">
              <Setter TargetName="Body" Property="Visibility" Value="Visible"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";
}